using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace SPINACH.Media
{

    public class ffmpegSession
    {
        public static string executablePath
        {
            get
            {
                var basePath = Application.streamingAssetsPath + "/SPINACH/Media/ffmpeg/{0}/ffmpeg{1}";
                var folder = string.Empty;
                var ext = string.Empty;
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        folder = "macOS";
                        break;

                    case RuntimePlatform.LinuxPlayer:
                    case RuntimePlatform.LinuxEditor:
                        folder = "Linux";
                        break;

                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        folder = "Windows";
                        ext = ".exe";
                        break;
                    
                    case RuntimePlatform.Android:
                        var path = "/data/local/tmp/ffmpeg";
                        return path;

                    default:
                        throw new SystemException("No ffmpeg executable present on current platform.");
                        // break;
                }

                return string.Format(basePath, folder, ext);
            }
        }

        public static bool executablePresent
        {
            get
            {
                var path = string.Empty;
                try
                {
                    path = executablePath;
                    UnityEngine.Debug.Log(string.Format("ffmpeg executable path is: {0}", path));
                }
                catch (Exception e)
                {
                    var str = e.Message; // to elminate the annoying warning
                    return false;
                }

                //  If the caller does not have sufficient permissions 
                // to read the specified file, no exception is thrown 
                // and the method returns false regardless of the existence of path.
                return File.Exists(path); 
            }
        }

        public string _arguments { get; private set; }
        private Process _sessionProcess;
        private bool _started;
        private bool _pipeStdout;
        public StreamWriter stdin { get; private set; }
        public StreamReader stdout { get; private set; }
        public StreamReader stderr { get; private set; }

        public Stream inputStream
        {
            get
            {
                if (!_started) throw new Exception("fuck off");
                return stdin.BaseStream;
            }
        }

        public Stream outputStream
        {
            get
            {
                if (!_started || !_pipeStdout) throw new Exception("fuck off");
                return stdout.BaseStream;
            }
        }

        public ffmpegSession(string argu, bool piped)
        {
            // if (!executablePresent)
            // {
            //     throw new SystemException("ffmpeg executable missing. reinstall the application or bash developers :)");
            // }

            _pipeStdout = piped;
            _arguments = argu;
            _started = false;

            if(Application.platform == RuntimePlatform.Android)
            {
                var newArguments = string.Format("-c \" ./data/local/tmp/ffmpeg {0} \" ", _arguments);
                UnityEngine.Debug.Log(newArguments);
                
                _sessionProcess = new Process();
                _sessionProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "/sbin/su",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = _pipeStdout,
                    RedirectStandardError = true, // if true, redirect error info to /storage/emulated/0/log.txt
                    Arguments = string.Format("-c \" ./data/local/tmp/ffmpeg/ffmpeg_API24_V431 {0} \" ", _arguments)
                    // Arguments = "-c \" ./data/local/tmp/ffmpeg -encoders \" "
                };

                Thread cfp = new Thread(checkffmpegProcess);
                cfp.Start();
            }
            else
            {
                _sessionProcess = new Process();
                _sessionProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = _pipeStdout,
                    RedirectStandardError = false,
                    Arguments = _arguments
                };
            }
            
        }

        public void Launch()
        {
            if (_started) return;

            _sessionProcess.Start();
            _started = true;

            stdin = _sessionProcess.StandardInput;
            stdout = _sessionProcess.StandardOutput;
            // stderr = _sessionProcess.StandardError;
        }

        /// <summary>
        ///  stop ffmpeg process and return its output.
        /// </summary>
        /// <returns></returns>
        public string EndSession()
        {
            stdin.Close();

            try
            {
                _sessionProcess.Kill();
            }
            catch (Exception e) 
            { /* kill the process silently.*/
                var str = e.Message; // to elminate the annoying warning
            }

            _sessionProcess.WaitForExit();
            _sessionProcess.Close();
            _sessionProcess.Dispose();

            return _pipeStdout ? stderr.ReadToEnd() : stdout.ReadToEnd();
        }

        void checkffmpegProcess()
        {
            while(true)
            {
                // Process[] pname = Process.GetProcessesByName("su");
                // if (pname.Length == 0)
                //     UnityEngine.Debug.Log("ffmpeg fail to launch");
                // else
                //     UnityEngine.Debug.Log("ffmpeg is running");

                Process[] pname = Process.GetProcessesByName("su");
                if (pname.Length != 0)
                    UnityEngine.Debug.Log("ffmpeg is running");
 
                // Process[] processlist = Process.GetProcesses();
                // foreach(Process theprocess in processlist){
                //     UnityEngine.Debug.Log(string.Format("Process: {0} ID: {1}", theprocess.ProcessName, theprocess.Id));
                // }
            }
        }

    }

}