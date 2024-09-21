using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Controls.Ribbon;
using Microsoft.Win32;

namespace BatchProcessApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow, IMessageFilter
    {
        [DllImport("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IMessageFilter lpMessageFilter, out IMessageFilter lplpMessageFilter);

        public MainWindow()
        {
            InitializeComponent();

            CoRegisterMessageFilter(this, out IMessageFilter oldFilter);

            DWGControl.HostApplication = this;

            try
            {
                RegistryKey key = Registry.ClassesRoot.CreateSubKey(".bpl"); key.SetValue("", "BPL", RegistryValueKind.String);
                key.Close();

                key = Registry.ClassesRoot.CreateSubKey("BPL\\shell\\open\\command");
                key.SetValue("", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"" + "\"\"%l\"\"", RegistryValueKind.String);
                key.Close();

                key = Registry.ClassesRoot.CreateSubKey("BPL\\DefaultIcon");
                key.SetValue("", System.Reflection.Assembly.GetExecutingAssembly().Location + ",-32512");
                key.Close();
            }
            catch { }
        }

        private bool _IsProcessRunning = false;
        private const string _toolTitle = "ScriptPro";

        #region IMessageFilter Members

        int IMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            return 0; // SERVERCALL_ISHANDLED 服务器调用已处理
        }

        int IMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            return 1000; // Retry in a second
        }

        int IMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        {
            return 1; // PENDINGMSG_WAITNOPROCESS 等待进程中的待处理消息
        }

        #endregion IMessageFilter Members

        private void AddDWGFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.AddDWGFiles();
            }
            catch { }
        }

        private void AddDWGFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.AddDWGFilesFromFolder();
            }
            catch { }
        }

        private void RemoveDWG_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.RemoveSelectedDWG();
            }
            catch { }
        }

        private void SkipDWG_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.SkipSelectedDWG();
            }
            catch { }
        }

        private void LoadList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                MessageBoxResult result = saveDrawingList();
                if (result == MessageBoxResult.Cancel) return;

                DWGControl.loadDWGList();

                if (!string.IsNullOrEmpty(DWGControl.ProjectName)) Title = _toolTitle + "  -  " + DWGControl.ProjectName;
            }
            catch { }
        }

        private void SaveList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.saveDWGList(false);

                if (!string.IsNullOrEmpty(DWGControl.ProjectName)) Title = _toolTitle + "  -  " + DWGControl.ProjectName;
            }
            catch { }
        }

        private void LoadSCP_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                MessageBoxResult result = saveDrawingList();
                if (result == MessageBoxResult.Cancel) return;

                DWGControl.loadFromSCPfile();
            }
            catch { }
        }

        private void RunChecked_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.runCheckedFiles();
            }
            catch { }
        }

        private void RunSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.runSelectedFiles();
            }
            catch { }
        }

        private void RunFailed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.runFailedFiles();
            }
            catch { }
        }

        private void StopProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DWGControl.stopProcess();
            }
            catch { }
        }

        private void ProcessOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.setOptions();
            }
            catch { }
        }

        private void updateUI(bool enabled)
        {
            try
            {
                NewList.IsEnabled = enabled;
                WizardList.IsEnabled = enabled;
                LoadList.IsEnabled = enabled;
                SaveList.IsEnabled = enabled;
                SaveAsList.IsEnabled = enabled;
                LoadSCP.IsEnabled = enabled;

                AddDWGFile.IsEnabled = enabled;
                AddDWGFolder.IsEnabled = enabled;
                RemoveDWG.IsEnabled = enabled;
                SkipDWG.IsEnabled = enabled;

                RunChecked.IsEnabled = enabled;
                RunSelected.IsEnabled = enabled;
                RunFailed.IsEnabled = enabled;

                // Opposite....

                StopProcess.IsEnabled = !enabled;

                ProcessOptions.IsEnabled = enabled;
            }
            catch { }
        }

        //This is to end the ScriptPro application
        //used in Silent exit

        public void exitApplication()
        {
            Application.Current.Shutdown();
        }

        //This is done to set the focus back to scriptpro
        //application. some times, scriptpro is not able to kill
        //AutoCAD, when AutoCAD is topmost application

        public void setFocusToApplication()
        {
            try
            {
                WindowState state = WindowState;
                WindowState = WindowState.Minimized;

                WindowState = WindowState.Maximized;
                Activate();
                WindowState = state;
            }
            catch
            {
            }
        }

        public void processStatus(bool started)
        {
            try
            {
                _IsProcessRunning = started;
                updateUI(!_IsProcessRunning);
            }
            catch { }
        }

        private void RibbonWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(DWGControl.ProjectName)) Title = _toolTitle + "  -  " + DWGControl.ProjectName;

                // Disable the stop button...

                StopProcess.IsEnabled = false;

                DWGControl.DoInitialize();
            }
            catch { }
        }

        private void NewList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                MessageBoxResult result = saveDrawingList();
                if (result == MessageBoxResult.Cancel) return;

                DWGControl.newDWGList();

                Title = _toolTitle;
            }
            catch
            {
            }
        }

        private void SaveAsList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                DWGControl.saveDWGList(true);

                if (DWGControl.ProjectName.Length != 0) Title = _toolTitle + "  -  " + DWGControl.ProjectName;
            }
            catch { }
        }

        private MessageBoxResult saveDrawingList()
        {
            MessageBoxResult result = MessageBoxResult.None;
            try
            {
                if (DWGControl.Modified)
                {
                    result = MessageBox.Show("Drawing list is modified. Do you want to save?", _toolTitle, MessageBoxButton.YesNoCancel);

                    if (result == MessageBoxResult.Cancel) return result;

                    if (result == MessageBoxResult.Yes) DWGControl.saveDWGList(false);
                }
            }
            catch
            {
            }
            return result;
        }

        private void RibbonWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                MessageBoxResult result = saveDrawingList();

                if (result == MessageBoxResult.Cancel) e.Cancel = true;
            }
            catch
            {
            }
        }

        private void ProcessHelp_Click(object sender, RoutedEventArgs e)
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            bool helpFileFound = false;
            string exeFolder = System.IO.Path.GetDirectoryName(exePath);
            string root = System.IO.Directory.GetDirectoryRoot(exePath);
            string helpFile = "";

            try
            {
                //helpFile = exeFolder + "\\" + "ReadMe.txt";
                helpFile = exeFolder + "\\" + "ScriptPro2.htm";
                if (System.IO.File.Exists(helpFile))
                {
                    helpFileFound = true;
                }
                else
                {
                    // Go one above

                    exeFolder = System.IO.Directory.GetParent(exeFolder).FullName;
                    //helpFile = exeFolder + "\\" + "ReadMe.txt";
                    helpFile = exeFolder + "\\" + "ScriptPro2.htm";
                    if (System.IO.File.Exists(helpFile))
                    {
                        helpFileFound = true;
                    }
                }
            }
            catch
            {
            }

            if (helpFileFound)
            {
                System.Diagnostics.Process.Start(helpFile);

                //System.Diagnostics.Process notePad =
                //  new System.Diagnostics.Process();
                //notePad.StartInfo.FileName = "notepad.exe";
                //notePad.StartInfo.Arguments = helpFile;
                //notePad.Start();
            }
            else
            {
                string strFolder = System.IO.Path.GetDirectoryName(exePath);
                MessageBox.Show("ReadMe.txt file not found at location " + strFolder, _toolTitle);
            }
        }

        private void WizardList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_IsProcessRunning) return;

                MessageBoxResult result = saveDrawingList();
                if (result == MessageBoxResult.Cancel) return;

                DWGControl.wizardDWGList();
            }
            catch
            {
            }
        }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000016-0000-0000-C000-000000000046")]
    public interface IMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }
}