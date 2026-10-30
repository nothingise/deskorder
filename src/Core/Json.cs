using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DeskOrder.Core
{
    public static class Json
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static T Read<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return null;
                return Serializer.Deserialize<T>(text);
            }
            catch (Exception ex)
            {
                Log.Error("Cannot read " + path, ex);
                try
                {
                    string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    File.Copy(path, backup, true);
                }
                catch { }
                return null;
            }
        }

        public static void Write<T>(string path, T value)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) AppPaths.Ensure(dir);
            string tmp = path + ".tmp";
            string text = Serializer.Serialize(value);
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); }
                catch { File.Copy(tmp, path, true); File.Delete(tmp); }
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        public static string ToJson(object value)
        {
            return Serializer.Serialize(value);
        }

        public static T FromJson<T>(string text) where T : class
        {
            return Serializer.Deserialize<T>(text);
        }
    }
}
