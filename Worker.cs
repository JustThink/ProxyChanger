using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Timers;
using NLog;

namespace ProxyChanger
{
	internal class Worker
	{
		private readonly Logger _log;
		private readonly Timer _timer;

		private bool _runnig;
		private double _timeout;
		private string _address;
		private string _downloadFileName;
		private string _login;
		private string _password;
		private string _prefix;
		private string _fileName;
        private int _size;
        private int _rows;
	    private readonly ProxyList _proxyList;

		public Worker(Logger Log)
		{
			_log = Log;

            _proxyList = new ProxyList(_log);

			_timer = new Timer();
			_timer.Elapsed += ElapsedEvent;
			_timer.Stop();
		}

		public bool Run()
		{
			if ( _runnig )
				return false;
			if ( !Initialization() )
				return false;
			if ( !Runnig() )
				return false;
			return true;
		}

		public void Stop()
		{
			if ( !_runnig )
				return;
			_timer.Stop();
		}

		private bool Initialization()
		{
			_log.Debug("Initialization...");

			try
			{
				_timeout = double.Parse(ConfigurationManager.AppSettings["timeout"]);
				_address = ConfigurationManager.AppSettings["address"];
				_downloadFileName = ConfigurationManager.AppSettings["downloadFileName"];
				_login = ConfigurationManager.AppSettings["login"];
				_password = ConfigurationManager.AppSettings["password"];
				_prefix = ConfigurationManager.AppSettings["prefix"];
				_fileName = ConfigurationManager.AppSettings["fileName"];
                _size = int.Parse(ConfigurationManager.AppSettings["size"]);
                _rows = int.Parse(ConfigurationManager.AppSettings["rows"]);

			    if (!_proxyList.Initialization()) return false;
			}
			catch ( Exception e )
			{
				_log.ErrorException("Error when get app settings", e);
				return false;
			}
			return true;
		}

		private bool Runnig()
		{
			_log.Debug("Runnig...");

			try
			{
				_timer.Interval = _timeout;
				_timer.Start();
				ElapsedEvent(this, null);
			}
			catch ( Exception e )
			{
				_log.ErrorException("Error when runnig", e);
				return false;
			}
			return true;
		}

		private void ElapsedEvent(object sender, ElapsedEventArgs e)
		{
			_timer.Stop();
			_log.Info("Doing...");


			Doing();

			_log.Info("Done.");
			_timer.Start();
		}

		private bool Doing()
		{
			if ( !DownloadFile() )
				return false;
			if ( !IsExists() )
				return false;

            _proxyList.Loading();
			var lines = GetLines();
			if ( lines == null )
			{
				_log.Error("Unable to read lines from a file {0}", _downloadFileName);
				return false;
			}
			if (lines.Length == 0)
			{
				_log.Debug("Skip step...");
				return true;
			}
		    if (lines.Length <= _rows)
		    {
                _log.Debug("The source file contains too few rows...");
                return true;
		    }
            _proxyList.AddProxyIfNotExists(lines);
            _proxyList.Save();

            GetRamdomLines(lines, _size);
            if (!WriteLines(lines, _size))
			{
				_log.Error("Unable to write lines to a file {0}", _fileName);
				return false;
			}
			return true;
		}

		private bool DownloadFile()
		{
			_log.Debug("Download file {0}", _address);

			var client = new WebClient();
			try
			{
				client.DownloadFile(_address, _downloadFileName);
			}
			catch ( Exception e )
			{
				_log.ErrorException("Error when download file", e);
				return false;
			}
			return true;

		}

		private bool IsExists()
		{
			_log.Debug("Check whether a file exists {0}", _downloadFileName);

			var exists = File.Exists(_downloadFileName);
			if ( !exists )
			{
				_log.Error("File {0} not found", _downloadFileName);
				return false;
			}
			return true;
		}

		private string[] GetLines()
		{
			_log.Debug("Reading lines from a file {0}", _downloadFileName);

			StreamReader file = null;
			string[] items = null;
			try
			{
				file = new StreamReader(_downloadFileName);
				string line = file.ReadToEnd();
				items = line.Split('\n');
			}
			catch ( Exception e )
			{
				_log.ErrorException("Error when working with a file", e);
			}
			finally
			{
				if ( file != null )
					file.Close();
			}

			if (( items == null ) || (items.Length == 0)) return null;


			string[] lines = null;
			try
			{
				var collection = new List<string>();
				foreach ( var item in items )
				{
					if ( !string.IsNullOrEmpty(item) )
					{
						var str = item.Trim();
						if ( !string.IsNullOrEmpty(str) )
						{
							collection.Add(str);
						}
					}
				}
				lines = collection.ToArray();
			}
			catch (Exception e)
			{
				_log.ErrorException("Error when parse lines", e);
			}
			return lines;
		}

        private static void GetRamdomLines(string[] lines, int size)
	    {
	        int pos = 0;
            int lenght = Math.Min(size, lines.Length);

            var rnd = new Random(DateTime.UtcNow.Millisecond);
            while (pos < lenght)
	        {
                var no = rnd.Next(lines.Length - pos);

	            string tmp = lines[pos];
                lines[pos] = lines[pos + no];
                lines[pos + no] = tmp;
	            pos++;
	        }
	    }

        private bool WriteLines(string[] lines, int size)
        {
            _log.Debug("Writing lines to a file {0}", _fileName);

            bool rc = false;
            FileMode mode = FileMode.CreateNew;
            if (File.Exists(_fileName))
                mode = FileMode.Truncate;

            FileLocker.Lock(_fileName, 1000, (fileStream) =>
            {
                try
                {
                    using (var file = new StreamWriter(fileStream))
                    {
                        bool withLogin = !string.IsNullOrEmpty(_login) && !string.IsNullOrEmpty(_password);

                        for (int i = 0; i < size && i < lines.Length; i++)
                        {
                            int last = size - 1;
                            string line = lines[i];
                            if (!_proxyList.CanUse(line))
                            {
                                size++; // пропускаем прокси
                                continue;
                            }
                            var values = line.Split('\t');
                            var value = _prefix + values[0];
                            if (i != last || withLogin)
                                file.WriteLine(value);
                            else
                            {
                                file.Write(value);
                            }
                        }

                        if (withLogin)
                        {
                            file.Write(_login + ":" + _password);
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
	}
}
