using System;
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

		public Worker(Logger Log)
		{
			_log = Log;

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

			var lines = GetLines();
			if ( lines == null )
			{
				_log.Error("Unable to read lines from a file {0}", _downloadFileName);
				return false;
			}

			var line = GetRamdomLine(lines);
			if ( !WriteLines(line) )
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
			string[] lines = null;
			try
			{
				file = new StreamReader(_downloadFileName);
				string line = file.ReadToEnd();
				lines = line.Split('\n');
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
			return lines;
		}

		private string GetRamdomLine(string[] lines)
		{
			var rnd = new Random(DateTime.UtcNow.Millisecond);
			var no = rnd.Next(lines.Length);
			return lines[no];
		}

		private bool WriteLines(string line)
		{
			_log.Debug("Writing lines to a file {0}", _fileName);

			string[] lines =
                {
                    _prefix + line,
                    _login + ":" + _password,
                };
			try
			{
				File.WriteAllLines(_fileName, lines);
			}
			catch ( Exception e )
			{
				_log.ErrorException("Error when write file", e);
				return false;
			}
			return true;
		}
	}
}
