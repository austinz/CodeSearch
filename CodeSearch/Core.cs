using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Nest;

namespace CodeSearch
{
    public class Core
    {
        private ElasticClient _clientInstance;
        private string _index;
        private string _type;
        private Action<string> _log;

        public Core(string serverUri,string index,string type)
        {
            var setting = new ConnectionSettings(new Uri(serverUri));
            
            _index = index;

            _type = type;

            setting.SetDefaultIndex(_index);

            _clientInstance = new ElasticClient(setting);
        }

        public ElasticClient ClientInstance
        {
            get
            {
                return _clientInstance;
            }
            set
            {
                _clientInstance = value;
            }
        }

        public void Start(string rootPath,Action<string> log)
        {
            _log = log;
            Remapping(_index, _type);
            TraverseFolder(rootPath);
        }

        private void TraverseFolder(string rootPath)
        {
            var directory = new DirectoryInfo(rootPath);

            if (!directory.Exists)
            {
                return;
            }

            var subDirectorys = directory.GetDirectories().ToList();
            var files = directory.GetFiles().ToList();

            if (files.Count > 0)
            {
                Parallel.ForEach(files, f =>
                {
                    _log("开始处理文件" + f.FullName + "------" + Thread.CurrentThread.ManagedThreadId);
                    PatitionFile(f);

                });
            }

            if (subDirectorys.Count > 0)
            {
                Parallel.ForEach(subDirectorys, d =>
                {
                    _log("--------开始处理目录" + d.FullName);
                    TraverseFolder(d.FullName);
                });
            }
        }

        private void PatitionFile(FileInfo fi)
        {
            if (fi.Exists && InWhieList(fi))
            {
                using (var stream = fi.OpenRead())
                {
                    using (var reader = new StreamReader(stream, Encoding.ASCII))
                    {
                        var lines = new List<string>();
                        var ns = string.Empty;
                        while (reader.Peek() >= 0)
                        {
                            var lineContent = reader.ReadLine().Trim();
                            if (lineContent.Length > 0)
                            {
                                try
                                {
                                    lines.Add(lineContent);
                                    if (lineContent.StartsWith("namespace", StringComparison.OrdinalIgnoreCase)) 
                                    {
                                        ns = lineContent.Substring(lineContent.LastIndexOf("namespace") + "namespace".Length);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _log(">>>>>exception:" + ex.Message);
                                }
                            }
                        }


                        Parallel.ForEach(lines, f =>
                        {
                            var info = new FileInformation(fi) 
                            {
                                Content=f,
                                LineNumber=lines.IndexOf(f) + 1,
                                NameSpace=ns
                            };

                            IndexFile(info, c => { Console.WriteLine(c); });

                        });

                    }
                }
            }
        }

        private void IndexFile(FileInformation fi, Action<string> log)
        {
            try
            {
                log("indexing..." + fi.FilePath);
                _clientInstance.Index(fi, "filerepo", "fileinformation");
            }
            catch (Exception ex)
            {
                log(">>>>>>exception" + ex.Message);
            }
        }

        private bool InWhieList(FileInfo fi)
        {
            var whiteList = new string[] { ".cs" };

            return whiteList.Contains(fi.Extension.ToLower());
        }

        private void Remapping(string index, string type)
        {
            _clientInstance.DeleteMapping<FileInformation>("filerepo");

            _clientInstance.MapFluent<FileInformation>(m => m.MapFromAttributes());
        }

         

    }

    [ElasticType(Name = "fileinformation")]
    public class FileInformation
    {
        [ElasticProperty(Name = "filename",Type=FieldType.string_type,Analyzer="filename_analyzer")]
        public string FileName { get; set; }
        [ElasticProperty(Name = "filepath", Type = FieldType.string_type, Analyzer = "path_analyzer")]
        public string FilePath { get; set; }
        //[ElasticProperty(Name = "directory", Type = FieldType.string_type, Analyzer = "standard")]
        //public string Directory { get; set; }
        [ElasticProperty(Name = "content", Type = FieldType.string_type, Analyzer = "content_analyzer")]
        public string Content { get; set; }
        [ElasticProperty(Name = "linenumber", Type = FieldType.integer_type, Index=FieldIndexOption.not_analyzed)]
        public int LineNumber { get; set; }
        [ElasticProperty(Name="namespace",Type=FieldType.string_type,Analyzer="ns_analyzer")]
        public string NameSpace { get; set; }

        public FileInformation() { }

        public FileInformation(FileInfo fi)
        {
            this.FileName = fi.Name;
            this.FilePath = fi.FullName;
            
        }

        public static FileInformation LoadFromFileInfo(FileInfo fi)
        {
            if (!fi.Exists)
            {
                return null;
            }
            using (var stream = fi.OpenRead())
            {
                using (var reader = new StreamReader(stream, Encoding.Default))
                {
                    var fInfo = new FileInformation(fi);
                    return fInfo;
                }
            }
        }
    }

}
