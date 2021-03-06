﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace rcloneExplorer
{
  public class rcloneExplorerInternalExec
  {
    IniFile iniSettings;
    rcloneExplorerDownloadHandler downloadsHandler;
    rcloneExplorerUploadHandler uploadsHandler;
    rcloneExplorerMiscContainer miscContainer;
    rcloneExplorerSyncHandler syncingHandler;   
    static string output = "";
    static Dictionary<int, string> errOutput = new Dictionary<int, string>();

    public void init()
    {
      iniSettings = rcloneExplorer.iniSettings;
      downloadsHandler = rcloneExplorer.downloadsHandler;
      uploadsHandler = rcloneExplorer.uploadsHandler;
      miscContainer = rcloneExplorer.miscContainer;
      syncingHandler = rcloneExplorer.syncingHandler;
    }

    public string Execute(string command, string arguments, string operation = null, string prepend = null, string[] rcmdlist = null)
    {
      output = null;
      string rcloneLogs = "";
      string threadIdTxt = DateTime.Now.Ticks.ToString().Substring(DateTime.Now.Ticks.ToString().Length - 9);
      int threadId = Convert.ToInt32(threadIdTxt);
      //check for verbose logging
      if (operation == "sync")
      {
        rcloneLogs = " --log-file sync.log --verbose";
      }
      else if (iniSettings.Read("rcloneVerbose") == "true")
      {
        rcloneLogs = " --log-file rclone.log --verbose";
      }

      //set up cmd to call rclone
      Process process = new Process();
      process.StartInfo.FileName = "cmd.exe";
      process.StartInfo.EnvironmentVariables["RCLONE_CONFIG_PASS"] = iniSettings.Read("rcloneConfigPass");
      process.StartInfo.Arguments = "/c " + prepend + "rclone.exe " + command + " " + arguments + rcloneLogs + " --stats 5s";
      process.StartInfo.CreateNoWindow = true;
      process.StartInfo.UseShellExecute = false;
      process.StartInfo.RedirectStandardError = true;
      process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
      process.StartInfo.RedirectStandardOutput = true;
      process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
      process.StartInfo.RedirectStandardInput = true;
      process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
      process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
      process.OutputDataReceived += (sender, args) => proc_OutputDataReceived(sender, args);
      process.ErrorDataReceived += (sender, args) => proc_ErrorDataReceived(sender, args, operation, threadId);
      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      //log command
      if (rcloneExplorer.consoleEnabled) {
        rcloneExplorer.rawOutputBuffer += "Thread: " + Thread.CurrentThread.ManagedThreadId + ": STDIN: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments + Environment.NewLine;
      }

      //log process ID for uploads, downloads and sync operations
      if (!String.IsNullOrEmpty(operation))
      {
        if (operation == "passcheck")
        {
            //check if config has password
            process.WaitForExit();
            if (output.Contains("password:"))
            {
                rcloneExplorer.configEncrypted = true;
            }
        }
        else if (operation == "up")
        {
          //log the process in the uploading list with race conditions galore
          int id = uploadsHandler.uploading.Count - 1;
          string percentage = uploadsHandler.uploading[id][2];
          string speed = uploadsHandler.uploading[id][3];
          //list should be {PID, Name, Percent, Speed}
          uploadsHandler.uploading[id][0] = process.Id.ToString();
          uploadsHandler.uploading[id][1] = "";
          uploadsHandler.uploading[id][2] = percentage;
          uploadsHandler.uploading[id][3] = speed;    
          //add entry in erroutput dictionary
          if (!errOutput.ContainsKey(threadId))
          {
              errOutput.Add(threadId, "");
          }               
          //monitor process output
          while (!process.HasExited)
          {
            if (errOutput[threadId] != null)
            {
                try
                {
                    percentage = Regex.Match(errOutput[threadId], @"\d+(?=%)% done", RegexOptions.RightToLeft).Value;
                    speed = Regex.Match(errOutput[threadId], @"[ \t]+\d+(.\d+)? [a-zA-Z]+[/s]s", RegexOptions.RightToLeft).Value;
                    if (!string.IsNullOrEmpty(percentage))
                    { uploadsHandler.uploading[id][2] = percentage; }
                    if (!string.IsNullOrEmpty(speed))
                    { uploadsHandler.uploading[id][3] = speed; }  
                }
                catch(NullReferenceException e)
                {
                    Console.Write(e);
                }
                catch(ArgumentNullException e)
                {
                    Console.Write(e);
                }
                errOutput[threadId] = null;
            }
          }
          if (process.HasExited)
          {
             uploadsHandler.uploading[id][2] = "100%";
             errOutput.Remove(threadId);
             return "";
          }
        }
        else if (operation == "down")
        {
          int id = downloadsHandler.downloading.Count - 1;
          string percentage = downloadsHandler.downloading[id][2];
          string speed = downloadsHandler.downloading[id][3];
          //list should be {PID, Name, Percent, Speed}
          downloadsHandler.downloading[id][0] = process.Id.ToString();
          downloadsHandler.downloading[id][1] = "";
          downloadsHandler.downloading[id][2] = percentage;
          downloadsHandler.downloading[id][3] = speed;
          //add entry in erroutput dictionary
          if (!errOutput.ContainsKey(threadId))
          {
              errOutput.Add(threadId, "");
          }
          //monitor process output
          while (!process.HasExited)
          {
              if (errOutput[threadId] != null)
              {
                  try
                  {
                      percentage = Regex.Match(errOutput[threadId], @"\d+(?=%)% done", RegexOptions.RightToLeft).Value;
                      speed = Regex.Match(errOutput[threadId], @"[ \t]+\d+(.\d+)? [a-zA-Z]+[/s]s", RegexOptions.RightToLeft).Value;
                      if (!string.IsNullOrEmpty(percentage))
                      { downloadsHandler.downloading[id][2] = percentage; }
                      if (!string.IsNullOrEmpty(speed))
                      { downloadsHandler.downloading[id][3] = speed; }  
                  }
                  catch (NullReferenceException e)
                  {
                      Console.Write(e);
                  }
                  catch (ArgumentNullException e)
                  {
                      Console.Write(e);
                  }
                  errOutput[threadId] = null;
              }
              
          }
          if (process.HasExited)
          {
             downloadsHandler.downloading[id][2] = "100%";
             errOutput.Remove(threadId);
             return "";
          }
        }
        else if (operation == "sync")
        {
          //log the process in the syncing list
          syncingHandler.syncingPID = process.Id;
        }
        else if (operation == "config")
        {

          //iterate through the commands needed to set the remote up (its different per remote)
          while (String.IsNullOrEmpty(output))
          {
            Thread.Sleep(100);
          }
          
          foreach (string rcmd in rcmdlist)
          {
            //the remote setup has asked to show a message
            if (rcmd.Contains("MSG: "))
            {
              while (!output.Contains("Got code"))
              {
                MessageBox.Show(rcmd);
              }
            }
            //the remote setup has hasked to open a url
            else if (rcmd.Contains("OPN|"))
            {
              //grab the predefined regex
              string regexp = rcmd.Split('|')[1];
              String url = "";
              while (String.IsNullOrEmpty(url))
              {
                url = Regex.Match(output, regexp).Value;
              }     
              //open the url
              Process.Start(url);
            }
            //the remote setup has asked for some information
            else if (rcmd.Contains("REQ:"))
            {
              string requiredinput = "";
              while (String.IsNullOrEmpty(requiredinput))
              {
                requiredinput = PromptGenerator.ShowDialog(rcmd, "");
              }
              process.StandardInput.WriteLine(requiredinput);
            }
            //the remote setup just wants to send some text
            else {
              process.StandardInput.WriteLine(rcmd);            
            }
            //sleep between operations
            System.Threading.Thread.Sleep(100);
          }
        }
      }
      else { process.WaitForExit(); } 
      
      //return output
      if (String.IsNullOrEmpty(output)) { output = ""; }
      return output.Replace("\r", "");
    }
    //process stdout
    static void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            output += e.Data.ToString() + Environment.NewLine;
            if (rcloneExplorer.consoleEnabled) {
                rcloneExplorer.rawOutputBuffer += "Thread: " + Thread.CurrentThread.ManagedThreadId + ": STDOUT: " + output;
            }
        }
    }
    //process stderr
    static void proc_ErrorDataReceived(object sender, DataReceivedEventArgs e, string operation, int threadId)
    {
        if (e.Data != null)
        {
            //add entry in erroutput dictionary
            if (!errOutput.ContainsKey(threadId))
            {
                errOutput.Add(threadId, "");
            }    
            //append data received
            if (operation == "up" || operation == "down")
            { errOutput[threadId] += e.Data.ToString() + Environment.NewLine; }
            //show in console if needed
            if (rcloneExplorer.consoleEnabled) {
                rcloneExplorer.rawOutputBuffer += "Thread: " + threadId + ": STDERR: " + errOutput[threadId];
            }
        }
    }

  }
    
}
