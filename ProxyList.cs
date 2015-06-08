using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace ProxyChanger
{
    internal class ProxyList
    {
        private readonly Logger _log;

        private string _use_file;
        private DateTime? _not_use;

        private readonly Dictionary<Int32, List<Int32>> _dictionary;

        public ProxyList(Logger Log)
        {
            _log = Log;
            _dictionary = new Dictionary<Int32, List<Int32>>();
        }

        public bool Initialization()
        {
            _log.Debug("Initialization 'Proxy List'...");
            try
            {
                _use_file = ConfigurationManager.AppSettings["use_file"];
                _not_use = GetDateTime(ConfigurationManager.AppSettings["not_use"]);
            }
			catch ( Exception e )
			{
				_log.ErrorException("Error when get app settings", e);
				return false;
			}
			return true;
        }

        private DateTime? GetDateTime(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string from = value.ToLower();
            if (from == "all") return null;
            try
            {
                var m = Regex.Match(value, @"^-(?<day>\d*?)d$");
                if (m.Success)
                {
                    var day = m.Groups["day"].Value;
                    int d;
                    if (int.TryParse(day, out d))
                    {
                        return DateTime.Today.AddDays(-d);
                    }
                }
            }
            catch { }
            _log.Warn("Not found 'DateTime': " + value);
            return null;
        }

        public bool Loading()
        {
            _log.Debug("Loading 'Proxy List'...");

            _log.Debug("Check whether a file exists {0}", _use_file);

            var exists = File.Exists(_use_file);
            if (!exists)
            {
                _log.Info("File {0} not found", _use_file);
                return true;
            }

            _log.Debug("Reading lines from a file {0}", _use_file);

            StreamReader file = null;
            string[] items = null;
            try
            {
                file = new StreamReader(_use_file);
                string line = file.ReadToEnd();
                items = line.Split('\n');
            }
            catch (Exception e)
            {
                _log.ErrorException("Error when working with a file", e);
            }
            finally
            {
                if (file != null)
                    file.Close();
            }

            if (( items == null ) || (items.Length == 0)) return false;

            _dictionary.Clear();
            foreach (var item in items)
            {
                if(string.IsNullOrEmpty(item)) continue;
                try
                {
                    var values = item.Split(';');
                    var d = values[0];
                    var p = values[1];


                    var date = ConvertToDateTime(d);
                    if (!date.HasValue)
                    {
                        _log.Error(string.Format("{0} isn't date", d));
                        break;
                    }

                    var h = p.Split('.');

                    Int32 di = Int32.Parse(d.Replace(".", ""));
                    if (!_dictionary.ContainsKey(di)) _dictionary[di] = new List<Int32>();

                    Byte[] bytes = new[]
                    {
                        Byte.Parse(h[0]),
                        Byte.Parse(h[1]),
                        Byte.Parse(h[2]),
                        Byte.Parse(h[3])
                    };

                    _dictionary[di].Add(BitConverter.ToInt32(bytes, 0));

                }
                catch (Exception e)
                {
                    _log.ErrorException("Error when working with a file", e);
                }
            }
            items = null;

            return true;
        }

        public bool AddProxyIfNotExists(string[] lines)
        {
            _log.Debug("Update 'Proxy List'...");

            Int32 di = DateToInt32(DateTime.UtcNow);


            foreach (var line in lines)
            {
                try
                {
                    Int32 t = StringToInt32(line);
                    bool found = false;
                    foreach (var proxyList in _dictionary.Values)
                    {
                        found = proxyList.Any(proxy => proxy == t);
                        if (found) break;
                    }

                    if (!found)
                    {
                        if (!_dictionary.ContainsKey(di)) _dictionary[di] = new List<Int32>();
                        _dictionary[di].Add(t);
                    }
                }
                catch (Exception e)
                {
                    _log.ErrorException("Error when add proxy: " + line, e);
                    break;
                }
            }
            return true;
        }

        public bool CanUse(string line)
        {
            if(!_not_use.HasValue) return true;

            Int32 t;
            try
            {
                t = StringToInt32(line);
            }
            catch (Exception e)
            {
                _log.ErrorException("Error when can proxy: " + line, e);
                return false;
            }

            Int32 di = DateToInt32(_not_use.Value);
            return _dictionary.Where(item => item.Key <= di).All(item => item.Value.All(p => p != t));
        }

        public bool Save()
        {
            bool rc = false;
            FileMode mode = FileMode.CreateNew;
            if (File.Exists(_use_file))
                mode = FileMode.Truncate;

            FileLocker.Lock(_use_file, 1000, (fileStream) =>
            {
                try
                {
                    using (var file = new StreamWriter(fileStream))
                    {
                        foreach (var d in _dictionary)
                        {
                            foreach (var h in d.Value)
                            {
                                Byte[] bytes = BitConverter.GetBytes(h);
                                string line = DateToString(d.Key) + ";" + BytesToString(bytes) + ";";
                                file.WriteLine(line);
                            }
                        }
                    }
                    rc = true;
                }
                finally
                {
                    fileStream.Close();
                    fileStream.Dispose();
                }
            }, mode, FileAccess.ReadWrite, FileShare.ReadWrite);
	        return rc;
        }

        private static Int32 DateToInt32(DateTime d)
        {
            return Int32.Parse(d.ToString("yyyyMMdd"));
        }

        private static string DateToString(Int32 di)
        {
            string d = di.ToString();
            return d.Insert(6, ".").Insert(4, ".");
        }

        private static Int32 StringToInt32(string line)
        {
            var l = line.Split('\t');
            var h = l[1].Split('.');
            Byte[] bytes = new[]
                    {
                        Byte.Parse(h[0]),
                        Byte.Parse(h[1]),
                        Byte.Parse(h[2]),
                        Byte.Parse(h[3])
                    };

            return BitConverter.ToInt32(bytes, 0);
        }

        private static string BytesToString(Byte[] bytes)
        {
            return string.Format("{0}.{1}.{2}.{3}", bytes[0], bytes[1], bytes[2], bytes[3]);
        }

        private const string DateTimeFormat = "yyyy.MM.dd";

        private static DateTime? ConvertToDateTime(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            DateTime dt;
            if (!DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return null;
            return dt;
        }
    }
}
