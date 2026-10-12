using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SaveLecture
{
    // Original timeline paths remain valid through this durable relocation journal.
    public sealed class SlideRelocations
    {
        readonly string root, sourceDirectory, removedDirectory, journal;
        readonly Dictionary<string,string> moved=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public SlideRelocations(string dayDirectory)
        {
            root=Path.GetFullPath(dayDirectory);
            sourceDirectory=Path.Combine(root,"scr"); removedDirectory=Path.Combine(root,"Removed");
            journal=Path.Combine(root,"_service","slides_relocations.jsonl");
            if(File.Exists(journal)) foreach(string line in File.ReadLines(journal)) {
                try {
                    var row=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);
                    if(row==null) continue;
                    string source=CheckedRelative(Convert.ToString(row["source"]),sourceDirectory);
                    string target=CheckedRelative(Convert.ToString(row["destination"]),removedDirectory);
                    if(source!=null && target!=null) moved[source]=target;
                } catch(ArgumentException) { } catch(KeyNotFoundException) { }
            }
        }
        string CheckedRelative(string relative,string directory)
        {
            if(String.IsNullOrEmpty(relative) || Path.IsPathRooted(relative)) return null;
            string absolute=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            return String.Equals(Path.GetDirectoryName(absolute),directory,StringComparison.OrdinalIgnoreCase)?absolute:null;
        }
        public string Resolve(string source)
        {
            string target;
            if(File.Exists(source)) return source;
            return moved.TryGetValue(source,out target) && File.Exists(target)?target:source;
        }
        public string Relative(string path)
        {
            string absolute=Path.GetFullPath(path), directory=Path.GetDirectoryName(absolute);
            if(String.Equals(directory,sourceDirectory,StringComparison.OrdinalIgnoreCase)) return "scr/"+Path.GetFileName(path);
            if(String.Equals(directory,removedDirectory,StringComparison.OrdinalIgnoreCase)) return "Removed/"+Path.GetFileName(path);
            throw new IOException("Image path is outside the recording folders.");
        }
        public string[] OriginalPaths()
        {
            var paths=new List<string>(Directory.GetFiles(sourceDirectory,"*.jpg"));
            foreach(string source in moved.Keys) if(!paths.Contains(source) && File.Exists(Resolve(source))) paths.Add(source);
            paths.Sort(StringComparer.Ordinal);
            return paths.ToArray();
        }
        public bool MoveEarlier(string source)
        {
            source=Path.GetFullPath(source);
            if(!String.Equals(Path.GetDirectoryName(source),sourceDirectory,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Only an image in this recording's scr folder can be moved.");
            if(!File.Exists(source)) return false;
            Directory.CreateDirectory(removedDirectory);
            string target=Path.Combine(removedDirectory,Path.GetFileName(source));
            while(File.Exists(target) || Directory.Exists(target)) target=Path.Combine(removedDirectory,Path.GetFileNameWithoutExtension(source)+"-"+Guid.NewGuid().ToString("N")+Path.GetExtension(source));
            Directory.CreateDirectory(Path.GetDirectoryName(journal));
            byte[] record=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(new {
                source=Relative(source), destination=Relative(target), observed=DateTime.Now.ToString("o"), reason="superseded_by_more_complete_slide"
            })+"\n");
            using(var stream=new FileStream(journal,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read)) {
                if(stream.Length>0) { stream.Position=stream.Length-1; if(stream.ReadByte()!=10) { stream.Position=stream.Length; stream.WriteByte(10); } }
                stream.Position=stream.Length; stream.Write(record,0,record.Length); stream.Flush(true);
            }
            moved[source]=target;
            File.Move(source,target);
            return true;
        }
    }
}
