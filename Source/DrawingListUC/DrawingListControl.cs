﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;

namespace DrawingListUC
{
    public partial class DrawingListControl : UserControl
    {
        public DrawingListControl()
        {
            InitializeComponent();
        }

        // Holds the host application - WPF application
        private object _hostApplication = null;

        // Active AutCAD object
        private object acadObject = null;

        // AutoCAD object id
        private string _acadObjectId = "";

        // Timeout for each drawing
        private int _timeoutSec = 10;

        // Start up script path
        private string _startUpScript = "";

        // Log file path
        private string _logFilePath = "";

        // Hold info about stoping the process
        private bool _stopBatchProcess = false;

        //Log file class
        private ReportLog bplog = null;

        // Thread which rund the batch process
        private BackgroundWorker batchProcessThread;

        // Timeout thread
        private BackgroundWorker _timeoutWk;

        // Timer to trigger AutoCAD restart
        private System.Timers.Timer _timeout = null;

        // AutoCAD restart count
        private int _restartDWGCount = 5;

        // Batch process options
        private int _runOption = 0;

        // Progress bar value
        private double pbValue = 0.0;

        // File count
        private int fileCount = 0;

        // Version in BPL file - for future use...
        //2.0 version
        //3.0 saving runWithoutOpen
        private const int BPLVersion = 3;

        // Options
        private const int RUN_CHECKED = 0;

        private const int RUN_SELECTED = 1;
        private const int RUN_FAILED = 2;

        private bool _checkAll = false;

        // Variable to flag killing of acad.
        private static bool _killAcad = false;

        // To hold filedia & recover mode value
        private object _fd = null;

        private object _rm = null;
        private object _lf = null;

        // Script path to run
        private string _scriptPath;

        // Current project opened
        private string _projectName = "";

        // AutoCAD name in string
        private string _acadProduct = "";

        private bool searchAllDirectories = false;

        private bool runWithoutOpen = false;

        private int createImage = 2;

        private bool diagnosticMode = false;

        //speed of the tool v2.1
        private int _toolSpeed = 0;

        // Flag to save the project modification status
        private bool _modified = false;

        // Temp color variable
        private Color itemColor;

        // Holds the info on batch process
        private bool _isProcessRunning = false;

        // Thread input class.
        private ThreadInput Threadinput = new ThreadInput();

        // Const strings and ints
        private const string dwgExt = "dwg";

        private const string dxfExt = "dxf";
        private const string currentDwg = "Current drawing is : ";

        private const string keyFolderName = "<acet:cFolderName>";
        private const string keyBaseName = "<acet:cBaseName>";
        private const string keyExtension = "<acet:cExtension>";
        private const string keyFileName = "<acet:cFileName>";
        private const string keyFullFileName = "<acet:cFullFileName>";

        private const int OPEN_NEW_DWG = 1;
        private const int CLOSE_DWG_SUCCESS = 2;
        private const int CLOSE_DWG_FAILED = 3;

        /// <summary>
        /// 获取系统变量常量
        /// </summary>
        private const string GetVariable = "GetVariable";

        /// <summary>
        /// 设置系统变量常量
        /// </summary>
        private const string SetVariable = "SetVariable";

        // AutoCAD location and size.
        private static int _left = 0;

        private static int _top = 0;
        private static int _width = 0;
        private static int _height = 0;

        private static string _currentDWG;
        private static bool _imageCreated;

        //AutoCAD exe to run before starting the application
        //using ActiveX API.
        private string acadExePath = "";

        //to run the selected version of application
        private bool runSelectedExe = false;

        //to exit the application without showing the logfile
        private bool silentExit = false;

        //to hold information on running script as commandline argument
        private bool useCmdLine = false;

        //wizard mode
        private bool wizardMode = false;

        // Some properties for the host application to use
        public bool Modified
        {
            set { _modified = value; }
            get { return _modified; }
        }

        public string ProjectName
        {
            set { _projectName = value; }
            get { return _projectName; }
        }

        public object HostApplication
        {
            set { _hostApplication = value; }
            get { return _hostApplication; }
        }

        #region UserInterface Members

        // Resize the controls
        private void DwgList_SizeChanged(object sender, EventArgs e)
        {
            // Control resize, set the col widths...
            int width = DwgList.Width;

            if (wizardMode)
            {
                // DWG name
                //DwgList.Columns[0].Width = (int)(width * 0.23);

                //// Path
                //DwgList.Columns[1].Width = (int)(width * 0.73);
            }
            else
            {
                // DWG name
                DwgList.Columns[0].Width = (int)(width * 0.25);

                // Path
                DwgList.Columns[1].Width = (int)(width * 0.6);

                // Status
                DwgList.Columns[2].Width = (int)(width * 0.15);
            }
        }

        public void DoInitialize()
        {
            // Make the process bar hidden by default
            BPbar.Visible = false;
            label_filename.Visible = false;

            ApplySettings();

            // Check the command line
            bool fileFound = false;
            bool startProcess = false;
            silentExit = false;

            string command = Environment.CommandLine;
            command = command.ToLower();

            if (!command.Contains(".bpl")) return;

            string[] args = Environment.GetCommandLineArgs();

            string strBPLname = "";
            foreach (string arg in args)
            {
                string str = arg.ToLower();

                if (str.Contains(".exe"))
                    continue;

                if (!fileFound)
                {
                    if (str.Contains(".bpl"))
                    {
                        strBPLname = strBPLname + str;
                        fileFound = true;
                    }
                    else
                    {
                        if (strBPLname.Length == 0)
                            strBPLname = str + " ";
                        else
                            strBPLname = strBPLname + str + " ";
                    }
                }
                if (fileFound && str.Contains("run"))
                    startProcess = true;

                if (startProcess == true && str.Contains("exit"))
                    silentExit = true; //exit application after running..
            }

            if (!File.Exists(strBPLname)) return;

            loadDWGList(strBPLname);
            updateControls();

            if (startProcess)
            {
                runCheckedFiles();
            }
        }

        private void updateControls()
        {
            try
            {
                ScriptPath.Text = _scriptPath;
            }
            catch
            {
                _scriptPath = "";
                _timeoutSec = 30;
                _startUpScript = "";
                _restartDWGCount = 30;
            }
        }

        public void ApplySettings()
        {
            _scriptPath = "";
            _timeoutSec = 30;
            _startUpScript = "";
            _restartDWGCount = 30;

            string str = Properties.Settings.Default.SearchAllDirectories;
            searchAllDirectories = !str.Contains("false");

            str = Properties.Settings.Default.CreateImage;

            if (str.Contains("1"))
                createImage = 1;
            else if (str.Contains("0"))
                createImage = 0;
            else
                createImage = 2;
        }

        public void AddDWGFilesFromFolder()
        {
            FolderBrowserDialog folderdg =
                new FolderBrowserDialog();
            folderdg.ShowNewFolderButton = false;
            if (folderdg.ShowDialog() ==
                DialogResult.OK)
            {
                SearchOption fileselection =
                  SearchOption.TopDirectoryOnly;
                if (searchAllDirectories)
                    fileselection =
                      SearchOption.AllDirectories;

                string[] dwgFiles =
                  Directory.GetFiles(
                    folderdg.SelectedPath + "\\",
                    "*." + dwgExt,
                    fileselection
                  );

                foreach (string fileName in dwgFiles)
                {
                    AddDWGtoView(fileName, true);
                }

                string[] dxfFiles =
                  Directory.GetFiles(
                    folderdg.SelectedPath + "\\",
                    "*." + dxfExt,
                    SearchOption.AllDirectories
                  );

                foreach (string fileName in dxfFiles)
                {
                    AddDWGtoView(fileName, true);
                }

                _modified = true;
            }
        }

        public void AddDWGFiles()
        {
            OpenFileDialog BPFileOpenDlg =
              new OpenFileDialog();
            BPFileOpenDlg.Filter = "Drawing Files (*.dwg, *.dxf)|*.dwg;*.dxf";
            BPFileOpenDlg.Multiselect = true;
            BPFileOpenDlg.Title = "Select files to add";

            if (BPFileOpenDlg.ShowDialog() == DialogResult.OK)
            {
                string[] FileNames =
                  BPFileOpenDlg.FileNames;

                foreach (string fileName in FileNames)
                {
                    AddDWGtoView(fileName, true);
                }

                _modified = true;
            }
        }

        private void ContextDWGAddFile_Click
            (object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem =
              sender as ToolStripMenuItem;

            if (menuItem.Name == "DWGAddFile" ||
                menuItem.Name == "ContextDWGAddFile")
                AddDWGFiles();
            else
                AddDWGFilesFromFolder();
        }

        private void ContextDWGAddFolder_Click(
          object sender, EventArgs e
        )
        {
            AddDWGFilesFromFolder();
        }

        private void AddDWGtoView(string fileName, bool bCheck)
        {
            string name = Path.GetFileName(fileName);
            string path = Path.GetDirectoryName(fileName);

            bool add = true;
            foreach (ListViewItem addedItem in DwgList.Items)
            {
                TagData tag = (TagData)addedItem.Tag;
                if (tag.DwgName == fileName)
                {
                    add = false;
                    break;
                }
            }

            if (add)
            {
                ListViewItem item = new ListViewItem(name, 0);
                item.Checked = bCheck;
                item.SubItems.Add(fileName);
                item.SubItems.Add("");
                TagData tag = new TagData();
                tag.DwgName = fileName;
                item.Tag = tag; // tag the item with file name
                DwgList.Items.Add(item);
            }
        }

        // Enable/disable the context menu items
        private void DwgContextMenu_Opening(
          object sender, CancelEventArgs e
        )
        {
            if (_isProcessRunning)
            {
                e.Cancel = true;
                return;
            }

            if (wizardMode)
            {
                DwgContextMenu.Items[8].Visible = false;
                DwgContextMenu.Items[7].Visible = false;
                DwgContextMenu.Items[6].Visible = false;
                DwgContextMenu.Items[5].Visible = false;
                DwgContextMenu.Items[4].Visible = false;
                DwgContextMenu.Items[3].Visible = false;
                DwgContextMenu.Items[2].Visible = false;
            }
            else
            {
                ToolStripMenuItem runStrip =
                  DwgContextMenu.Items[8] as ToolStripMenuItem;

                if (DwgList.SelectedItems.Count == 0)
                {
                    DwgContextMenu.Items[0].Enabled = true;
                    DwgContextMenu.Items[1].Enabled = false;
                    DwgContextMenu.Items[2].Enabled = false;

                    runStrip.DropDownItems[1].Enabled = false;
                }
                else
                {
                    DwgContextMenu.Items[0].Enabled = false;
                    DwgContextMenu.Items[1].Enabled = true;

                    runStrip.DropDownItems[1].Enabled = true;

                    ListView.SelectedListViewItemCollection selItems =
                      DwgList.SelectedItems;

                    ListViewItem firstItem = null;
                    bool bEnabled = true;

                    foreach (ListViewItem item in selItems)
                    {
                        if (firstItem == null)
                            firstItem = item;

                        if (firstItem.Checked != item.Checked)
                            bEnabled = false;
                    }

                    DwgContextMenu.Items[2].Enabled = bEnabled;

                    if (firstItem != null)
                    {
                        if (firstItem.Checked)
                            DwgContextMenu.Items[2].Text = "Skip";
                        else
                            DwgContextMenu.Items[2].Text = "Include";
                    }
                }

                runStrip.DropDownItems[0].Enabled =
                  (DwgList.CheckedItems.Count != 0);

                bool enabled = false;
                foreach (ListViewItem item in DwgList.Items)
                {
                    TagData data = (TagData)item.Tag;

                    if (!data.status)
                    {
                        enabled = true;
                        break;
                    }
                }

                runStrip.DropDownItems[2].Enabled = enabled;

                if (DwgList.Items.Count == 0)
                {
                    DwgContextMenu.Items[3].Enabled = false;
                    DwgContextMenu.Items[8].Enabled = false;
                }
                else
                {
                    DwgContextMenu.Items[3].Enabled = true;
                    DwgContextMenu.Items[8].Enabled = true;
                }
            }
        }

        // Select the script
        private void ScriptBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog BPFileOpenDlg = new OpenFileDialog();
            BPFileOpenDlg.Filter = "Script (*.scr) |*.scr;";
            if (BPFileOpenDlg.ShowDialog() == DialogResult.OK)
            {
                _scriptPath = BPFileOpenDlg.FileName;
                ScriptPath.Text = _scriptPath;
                _modified = true;
            }
        }

        // View the script
        private void Viewbutton_Click(object sender, EventArgs e)
        {
            Process notePad = new Process();
            notePad.StartInfo.FileName = "notepad.exe";

            // Find if the file present
            if (File.Exists(ScriptPath.Text))
                notePad.StartInfo.Arguments = ScriptPath.Text;
            notePad.Start();
        }

        // Remove the selected DWG
        public void RemoveSelectedDWG()
        {
            // Remove the drawings from the list control

            ListView.SelectedListViewItemCollection selItems =
              DwgList.SelectedItems;

            foreach (ListViewItem item in selItems)
            {
                // Remove the item from listview
                DwgList.Items.Remove(item);
            }

            _modified = true;
        }

        //
        private void RemoveDWG_Click(object sender, EventArgs e)
        {
            RemoveSelectedDWG();
        }

        // Mark the selected DWG as skip
        public void SkipSelectedDWG()
        {
            ListView.SelectedListViewItemCollection selItems =
              DwgList.SelectedItems;

            foreach (ListViewItem item in selItems)
            {
                // Remove the item from listview
                item.Checked = !item.Checked;
            }
            _modified = true;
        }

        //
        private void DwgList_ItemChecked(
          object sender, ItemCheckedEventArgs e
        )
        {
            _modified = true;
        }

        private void SkipDWG_Click(object sender,
            EventArgs e)
        {
            SkipSelectedDWG();
        }

        private void chToolStripMenuItem_Click(
          object sender, EventArgs e
        )
        {
            foreach (ListViewItem item in DwgList.Items)
            {
                item.Checked = _checkAll;
            }
            _checkAll = !_checkAll;
        }

        // Context menu options
        private void saveDWGListToolStripMenuItem_Click(
          object sender, EventArgs e
        )
        {
            saveDWGList(false);
        }

        public void setOptions()
        {
            OptionsDlg dlg = new OptionsDlg();

            dlg.setProjectSetting(_startUpScript, _timeoutSec.ToString(), _logFilePath, _restartDWGCount.ToString());

            dlg.DiagnosticMode = diagnosticMode;
            dlg.toolSpeed = Convert.ToInt32(_toolSpeed * 0.001);
            dlg.acadExePath = acadExePath;
            dlg.RunWithoutOpen = runWithoutOpen;
            dlg.UseScriptAsCmdLine = useCmdLine;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _startUpScript = dlg.IniScript;
                _timeoutSec = dlg.timeout;
                _logFilePath = dlg.logFilePath;
                _restartDWGCount = dlg.reStartCount;
                searchAllDirectories = dlg.SearchAllDirectories;
                runWithoutOpen = dlg.RunWithoutOpen;

                createImage = dlg.nCreateImage;
                diagnosticMode = dlg.DiagnosticMode;
                _toolSpeed = dlg.toolSpeed * 1000;

                //if (!dlg.acadExePath.Equals(acadExePath,
                //      StringComparison.OrdinalIgnoreCase))
                //{
                //    runSelectedExe = true;
                //}

                acadExePath = dlg.acadExePath;

                if (acadExePath.Length != 0)
                    runSelectedExe = true;

                useCmdLine = dlg.UseScriptAsCmdLine;

                _modified = true;
            }
        }

        #endregion UserInterface Members

        #region ReadDrawingList Members

        // Read the BPL file
        private void readGeneralSection(StreamReader SR)
        {
            string[] lines;
            try
            {
                // General_Start
                string version = "";
                string linetext = SR.ReadLine();

                // Read version,
                linetext = SR.ReadLine();
                lines = linetext.Split('*');
                version = lines[1];

                // Read product
                linetext = SR.ReadLine();
                lines = linetext.Split('*');
                _acadProduct = lines[1];

                // Read Script file
                linetext = SR.ReadLine();

                lines = linetext.Split('*');
                _scriptPath = lines[1];

                // Read timeout file
                linetext = SR.ReadLine();
                lines = linetext.Split('*');
                _timeoutSec = Convert.ToInt32(lines[1]);

                // Read ReStart file
                linetext = SR.ReadLine();
                lines = linetext.Split('*');
                _restartDWGCount = Convert.ToInt32(lines[1]);

                // Read start up file
                linetext = SR.ReadLine();
                lines = linetext.Split('*');
                _startUpScript = lines[1];

                // Read log file
                linetext = SR.ReadLine();
                lines = linetext.Split('*');
                _logFilePath = lines[1];

                int _ver = Convert.ToInt32(version);
                if (_ver >= 2) //for version 2 and higher
                {
                    //read the AutoCAD exe path
                    linetext = SR.ReadLine();
                    lines = linetext.Split('*');

                    string exePath = lines[1];

                    if (!exePath.Equals(acadExePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        runSelectedExe = true;
                    }
                    acadExePath = exePath;

                    //read the sleep amount
                    linetext = SR.ReadLine();
                    lines = linetext.Split('*');
                    _toolSpeed = Convert.ToInt32(lines[1]);
                }

                //for version 3 and higher
                runWithoutOpen = false;
                if (_ver >= 3)
                {
                    //check if the script to run on empty drawing
                    linetext = SR.ReadLine();
                    lines = linetext.Split('*');

                    string withOutfileOpen = lines[1];
                    runWithoutOpen = Convert.ToBoolean(withOutfileOpen);
                }

                useCmdLine = isHeadlessAcad(acadExePath);

                // Read General_End
                linetext = SR.ReadLine();
            }
            catch { }

            // Update the script file name
            ScriptPath.Text = _scriptPath;
        }

        // Load script pro project file...
        public void loadFromSCPfile()
        {
            // Clear the drawing list first...
            DwgList.Items.Clear();

            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter =
              "ScriptPro project files (*.scp) |*.scp;";
            openDlg.Title =
              "Load ScriptPro project files";

            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                StreamReader SR = File.OpenText(openDlg.FileName);
                string linetext = "";
                string[] lines;

                // General
                linetext = SR.ReadLine();

                // Complete
                linetext = SR.ReadLine();

                // Script file
                linetext = SR.ReadLine();
                lines = linetext.Split('=');
                _scriptPath = lines[1];
                ScriptPath.Text = _scriptPath;

                // Timeout
                linetext = SR.ReadLine();
                lines = linetext.Split('=');
                _timeoutSec = Convert.ToInt32(lines[1]);

                // Log...
                linetext = SR.ReadLine();

                // Log file name
                linetext = SR.ReadLine();
                lines = linetext.Split('=');

                _logFilePath = Path.GetDirectoryName(lines[1]);

                // Use UNC
                linetext = SR.ReadLine();

                // Read drawings...
                linetext = SR.ReadLine();
                while (linetext != null)
                {
                    if (linetext.Length != 0)
                    {
                        if (!linetext.Contains("[FileList]"))
                        {
                            lines = linetext.Split('\t');
                            string DWG = lines[1] + lines[0];

                            bool bcheeck = true;
                            if (lines[2].Contains("Skip"))
                            {
                                bcheeck = false;
                            }
                            AddDWGtoView(DWG, bcheeck);
                        }
                    }
                    linetext = SR.ReadLine();
                }
            }
        }

        // New list...
        public void newDWGList()
        {
            // Clear the drawing list
            DwgList.Items.Clear();

            // New project
            _projectName = "";
            _modified = false;
            acadExePath = "";
            runWithoutOpen = false;
            useCmdLine = false;
        }

        // Loads the passed drawing (bpl) file
        public void loadDWGList(string filename)
        {
            StreamReader SR = File.OpenText(filename);
            string linetext = "";
            try
            {
                readGeneralSection(SR);
            }
            catch
            {
                return;
            }

            string[] lines;

            // DWGList_Start
            linetext = SR.ReadLine();

            // First drawing
            linetext = SR.ReadLine();
            while (linetext != null)
            {
                if (linetext.Contains("DWGList_End"))
                    break;

                lines = linetext.Split(',');

                if (lines.Length == 2)
                {
                    if (Convert.ToInt32(lines[1]) == 0)
                        AddDWGtoView(lines[0], false);
                    else
                        AddDWGtoView(lines[0], true);
                }
                if (lines.Length == 1)
                {
                    AddDWGtoView(lines[0], true);
                }

                linetext = SR.ReadLine();
            }
            SR.Close();

            ProjectName = filename;
            _modified = false;
        }

        // Ask the user for the bpl file to load
        public void loadDWGList()
        {
            // Clear the drawing list first...
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "Drawing list (*.bpl) |*.bpl;";
            openDlg.Title = "Drawing list";

            if (File.Exists(_projectName))
                openDlg.InitialDirectory = Path.GetDirectoryName(_projectName);
            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                DwgList.Items.Clear();
                runWithoutOpen = false;
                useCmdLine = false;

                loadDWGList(openDlg.FileName);
            }
        }

        // Context menu options
        private void loadDWGListToolStripMenuItem_Click(
          object sender, EventArgs e
        )
        {
            loadDWGList();
        }

        public void writeDWGList(string strProjectName, bool failed)
        {
            try
            {
                StreamWriter sw = File.CreateText(strProjectName);

                // First write all the general infomation
                sw.WriteLine("[General_Start]");
                sw.WriteLine("Version*" + BPLVersion.ToString());
                sw.WriteLine("Product*" + "2011");
                sw.WriteLine("Script*" + _scriptPath);
                sw.WriteLine("TimeOut*" + _timeoutSec.ToString());
                sw.WriteLine("RestartCount*" + _restartDWGCount.ToString());
                sw.WriteLine("IniScript*" + _startUpScript);
                sw.WriteLine("LogFileName*" + _logFilePath);
                sw.WriteLine("AutoCADPath*" + acadExePath);
                sw.WriteLine("Sleep*" + _toolSpeed.ToString());
                sw.WriteLine("RunwithoutOpen*" + runWithoutOpen.ToString());

                sw.WriteLine("[General_End]");

                sw.WriteLine("[DWGList_Start]");
                foreach (ListViewItem item in DwgList.Items)
                {
                    TagData data = (TagData)item.Tag;
                    string strCheck = "0";
                    if (item.Checked)
                    {
                        strCheck = "1";
                    }
                    if (failed)
                    {
                        if (data.status == false)
                            sw.WriteLine(data.DwgName + "," + strCheck);
                    }
                    else
                    {
                        sw.WriteLine(data.DwgName + "," + strCheck);
                    }
                }
                sw.WriteLine("[DWGList_End]");

                sw.Close();
            }
            catch (Exception ex)
            {
                if (failed == false)
                    MessageBox.Show(ex.Message);
            }
        }

        // Save the BPL list, called from save as save as
        public void saveDWGList(bool saveAs)
        {
            bool showDialog = false;

            if (saveAs)
            {
                showDialog = true;
            }
            else
            {
                if (_projectName.Length == 0)
                    showDialog = true;
            }

            if (showDialog)
            {
                SaveFileDialog saveDlg = new SaveFileDialog();
                saveDlg.Filter = "Drawing list (*.bpl) |*.bpl;";
                saveDlg.Title = "Drawing list";
                saveDlg.OverwritePrompt = true;

                if (saveDlg.ShowDialog() == DialogResult.OK)
                {
                    _projectName = saveDlg.FileName;
                    writeDWGList(_projectName, false);
                }
            }
            _modified = false;
        }

        #endregion ReadDrawingList Members

        #region RunBatchProcess Members

        // Run the batch process for checked files
        public void runCheckedFiles()
        {
            if (DwgList.Items.Count == 0) return;

            if (DwgList.CheckedItems.Count == 0) return;

            // Remove the list
            Threadinput._FileInfolist.Clear();

            foreach (ListViewItem item in DwgList.Items)
            {
                if (item.Checked)
                {
                    FileInfo info = new FileInfo();
                    TagData data = (TagData)item.Tag;
                    info._fileName = data.DwgName;
                    info._index = item.Index;
                    Threadinput._FileInfolist.Add(info);
                }
            }

            _stopBatchProcess = false;
            StartBatchProcess(false, RUN_CHECKED);
        }

        // Context menu option
        private void toolStripMenuItem2_Click(object sender, EventArgs e
        )
        {
            runCheckedFiles();
        }

        // Run the selected DWG files
        public void runSelectedFiles()
        {
            if (DwgList.Items.Count == 0)
                return;

            if (DwgList.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                          "Try after selecting the required files",
                          "ScriptPro", MessageBoxButtons.OK
                        );

                return;
            }

            // Remove the list
            Threadinput._FileInfolist.Clear();

            ListView.SelectedListViewItemCollection selItems =
              DwgList.SelectedItems;

            foreach (ListViewItem item in selItems)
            {
                FileInfo info = new FileInfo();
                TagData data = (TagData)item.Tag;
                info._fileName = data.DwgName;
                info._index = item.Index;
                Threadinput._FileInfolist.Add(info);
            }
            _stopBatchProcess = false;

            StartBatchProcess(false, RUN_SELECTED);
        }

        // Context menu option to run the selected
        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            runSelectedFiles();
        }

        // Run only failed dwg files
        public void runFailedFiles()
        {
            if (DwgList.Items.Count == 0)
                return;

            // Remove the list
            Threadinput._FileInfolist.Clear();

            bool run = false;
            foreach (ListViewItem item in DwgList.Items)
            {
                TagData data = (TagData)item.Tag;
                if (!data.status)
                {
                    FileInfo info = new FileInfo();
                    info._fileName = data.DwgName;
                    info._index = item.Index;
                    item.SubItems[2].Text = "";
                    item.ForeColor = Color.Black;
                    Threadinput._FileInfolist.Add(info);
                    run = true;
                }
            }

            if (run)
            {
                _stopBatchProcess = false;
                StartBatchProcess(false, RUN_FAILED);
            }
            else
            {
                MessageBox.Show(
                "No failed files",
                "ScriptPro", MessageBoxButtons.OK
              );
            }
        }

        private void failToolStripMenuItem_Click(
          object sender, EventArgs e
        )
        {
            runFailedFiles();
        }

        public void stopProcess()
        {
            _stopBatchProcess = true;
        }

        #endregion RunBatchProcess Members

        #region AutoCAD_related Members

        // Initialize UI to start the batch process
        private void Initialize_start(int userOption)
        {
            BPbar.Visible = true;
            label_filename.Visible = true;

            UpdateHostApplicationUI(true);

            //make sure start the selected exe first..
            if (acadExePath.Length != 0) runSelectedExe = true;

            if (userOption == RUN_SELECTED)
            {
                ListView.SelectedListViewItemCollection selItems =
                  DwgList.SelectedItems;

                foreach (ListViewItem item in selItems)
                {
                    item.SubItems[2].Text = "";
                    item.ForeColor = Color.Black;
                }
            }
            else if (userOption == RUN_CHECKED)
            {
                foreach (ListViewItem item in DwgList.CheckedItems)
                {
                    item.SubItems[2].Text = "";
                    item.ForeColor = Color.Black;
                }
            }
            else if (userOption == RUN_FAILED)
            {
                foreach (ListViewItem item in DwgList.Items)
                {
                    TagData data = (TagData)item.Tag;
                    if (!data.status)
                    {
                        item.SubItems[2].Text = "";
                        item.ForeColor = Color.Black;
                    }
                }
            }

            DwgList.Refresh();

            if (_timeout == null && !useCmdLine)
            {
                _timeout = new System.Timers.Timer();
                _timeout.AutoReset = false;
                _timeout.Elapsed += new System.Timers.ElapsedEventHandler(_timeout_Elapsed);
            }

            // Start the timer
            if (_timeoutWk == null && !useCmdLine)
            {
                _timeoutWk = new BackgroundWorker();
                _timeoutWk.DoWork += new DoWorkEventHandler(_timeoutWk_DoWork);
                _timeoutWk.ProgressChanged += new ProgressChangedEventHandler(_timeoutWk_ProgressChanged);
                _timeoutWk.WorkerReportsProgress = true;
                _timeoutWk.WorkerSupportsCancellation = true;
                _timeoutWk.RunWorkerAsync(null);
            }

            if (batchProcessThread == null)
            {
                batchProcessThread = new BackgroundWorker();
                batchProcessThread.DoWork += new DoWorkEventHandler(batchProcessThread_DoWork);
                batchProcessThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(batchProcessThread_RunWorkerCompleted);
                batchProcessThread.ProgressChanged += new ProgressChangedEventHandler(batchProcessThread_ProgressChanged);
                batchProcessThread.WorkerReportsProgress = true;
                batchProcessThread.WorkerSupportsCancellation = true;
            }

            this.BPbar.Value = 0;
            label_filename.Text = "";

            if (_logFilePath.Length == 0)
            {
                // Set the user temp directory...
                _logFilePath = Path.GetTempPath();
            }

            try
            {
                if (Directory.Exists(_logFilePath))
                    bplog = new ReportLog(_logFilePath, _projectName);
            }
            catch
            {
                bplog = null;
            }
        }

        //function to check whether application
        //is a AutoCAD or headless exe (at present accoreconsole.exe)
        public static bool isHeadlessAcad(string strExePath)
        {
            string strFileName = Path.GetFileName(strExePath);
            strFileName = strFileName.ToLower();

            if (strFileName.Length == 0) return false;

            if (strFileName.Contains("acad.exe")) return false;

            return true;
        }

        // No try and catch, function which calls
        // this API should have try and catch...
        private bool startAutoCAD()
        {
            //No need to start the AutoCAD through ActiveX
            //user wants to run the script as command line
            //argument

            if (useCmdLine) return true;

            //now check the runSelectedExe. if false
            //run the selected AutoCAD exe so that
            //CreateInstance creates the correct exe.
            if (runSelectedExe)
            {
                if (File.Exists(acadExePath))
                {
                    //create the exe
                    try
                    {
                        //create tem scr file
                        string tempExitFile = Path.GetTempPath() + "tempExit.scr";
                        StreamWriter sw = new StreamWriter(tempExitFile, false);
                        sw.WriteLine("._quit");
                        sw.Close();
                        sw.Dispose();

                        string ApplicationArguments = string.Format("/b \"{0}\"", tempExitFile);

                        Process ProcessObj = new Process();
                        ProcessObj.StartInfo.FileName = acadExePath;
                        ProcessObj.StartInfo.Arguments = ApplicationArguments;
                        ProcessObj.StartInfo.UseShellExecute = false;
                        ProcessObj.StartInfo.CreateNoWindow = true;
                        ProcessObj.StartInfo.RedirectStandardOutput = true;

                        ProcessObj.Start();

                        //300 seconds...for slower machines
                        ProcessObj.WaitForExit(300000);

                        Process[] procs = Process.GetProcessesByName("acad");
                        foreach (Process proc in procs)
                        {
                            if (proc.Id == ProcessObj.Id)
                            {
                                Type myType = this.HostApplication.GetType();
                                MethodInfo myMethodInfo = myType.GetMethod("setFocusToApplication");
                                myMethodInfo.Invoke(HostApplication, null);
                                Application.DoEvents();

                                proc.Kill();
                                break;
                            }
                        }
                    }
                    catch { }
                }

                runSelectedExe = false;
            }

            string acad = "AutoCAD.Application";
            Type acType = Type.GetTypeFromProgID(acad);

            //try 3 times to start the AutoCAD.
            // for slow machines....
            int nTryAcad = 3;

            while (nTryAcad > 0)
            {
                try
                {
                    acadObject = Activator.CreateInstance(acType);
                    break;
                }
                catch
                {
                    nTryAcad--;

                    //unable to start AutoCAD
                    if (nTryAcad == 0) return false;

                    Thread.Sleep(30000); //30 seconds
                }
            }

            Thread.Sleep(1000 + _toolSpeed); // 1 sec

            object[] dataArry = new object[1];
            dataArry[0] = true;
            acadObject.GetType().InvokeMember("Visible", BindingFlags.SetProperty, null, acadObject, dataArry);

            object acadHwnd = acadObject.GetType().InvokeMember("HWND", BindingFlags.GetProperty, null, acadObject, null);

            _acadObjectId = acadHwnd.ToString();

            return true;
        }

        //This function finds the presence of Keywords and
        //nested scripts
        private int checkNestedScripts(string scriptFile)
        {
            StreamReader sr = new StreamReader(scriptFile);
            string str = sr.ReadToEnd();
            //str = str.ToLower();
            sr.Close();
            sr.Dispose();

            int nReturn = 0;
            try
            {
                if (str.Contains(keyFolderName) ||
                    str.Contains(keyBaseName) ||
                    str.Contains(keyExtension) ||
                    str.Contains(keyFileName) ||
                    str.Contains(keyFullFileName))
                {
                    nReturn = 1; //contains key word
                }

                if (str.Contains("call") || str.Contains("Call"))
                {
                    nReturn = 2; //nested script
                }
            }
            catch
            {
            }

            return nReturn;
        }

        //This function replaces Keywords.
        //
        private bool replaceKeyWords(string scriptFile, ref string newFile, string dwgName)
        {
            bool keyWordAdded = false;

            try
            {
                newFile = Path.GetTempPath() + "KeywordTemp.scr";

                StreamReader sr = new StreamReader(scriptFile);
                string str = sr.ReadToEnd();
                //str = str.ToLower();
                sr.Close();
                sr.Dispose();

                if (str.Contains(keyFolderName))
                {
                    string folder = Path.GetDirectoryName(dwgName);

                    DirectoryInfo info = Directory.GetParent(folder);

                    if (info != null)
                        str = str.Replace(keyFolderName, folder);
                    else
                    {
                        folder = folder.TrimEnd('\\');
                        str = str.Replace(keyFolderName, folder);
                    }

                    keyWordAdded = true;
                }

                if (str.Contains(keyBaseName))
                {
                    string basename = Path.GetFileNameWithoutExtension(dwgName);
                    str = str.Replace(keyBaseName, basename);
                    keyWordAdded = true;
                }

                if (str.Contains(keyExtension))
                {
                    string ext = Path.GetExtension(dwgName);
                    str = str.Replace(keyExtension, ext);
                    keyWordAdded = true;
                }

                if (str.Contains(keyFileName))
                {
                    string name = Path.GetFileName(dwgName);
                    str = str.Replace(keyFileName, name);
                    keyWordAdded = true;
                }

                if (str.Contains(keyFullFileName))
                {
                    //string fullName = Path.GetFileName(dwgName);
                    str = str.Replace(keyFullFileName, dwgName);
                    keyWordAdded = true;
                }

                if (keyWordAdded)
                {
                    StreamWriter sw = new StreamWriter(newFile, false);
                    sw.Write(str);
                    sw.Close();
                    sw.Dispose();
                }
            }
            catch
            {
                keyWordAdded = false;
            }

            return keyWordAdded;
        }

        // This function will create a new script file in
        // the user's temp Directory for nested scripts only
        private bool processNestedScripts(string scriptPath, string scriptFile, ref string newFile, ref bool errorInScript)
        {
            bool useTemp = false;
            try
            {
                string tempFile = Path.GetTempPath() + "NestedTemp1.scr";
                StreamReader sr = File.OpenText(scriptFile);
                StreamWriter sw = new StreamWriter(tempFile, false);
                string linetext = "";
                string scriptFolder = Path.GetDirectoryName(scriptFile);

                try
                {
                    linetext = sr.ReadLine();

                    while (linetext != null)
                    {
                        //string strLower = linetext.ToLower();
                        if (linetext.Contains("call") || linetext.Contains("Call"))
                        {
                            //
                            string[] lines = linetext.Split(' ');

                            if (string.Compare(lines[0], "call") != 0 &&
                                string.Compare(lines[0], "Call") != 0)
                            {
                                sw.WriteLine(linetext);
                            }
                            else
                            {
                                string strNested = null;

                                string strFileName = "";
                                int nLength = lines.Length;

                                for (int nString = 1; nString < nLength; nString++)
                                {
                                    if (strFileName.Length == 0)
                                        strFileName = lines[nString];
                                    else
                                        strFileName = strFileName + " " + lines[nString];
                                }

                                if (File.Exists(strFileName))
                                    strNested = strFileName;
                                else //relative path...
                                    strNested = scriptPath + "\\" + strFileName;

                                //Find the nested file.
                                //if failed to find, then write the complete
                                //line to script. This will be error...
                                if (File.Exists(strNested))
                                {
                                    StreamReader srNested = File.OpenText(strNested);

                                    string textNested = srNested.ReadLine();

                                    while (textNested != null)
                                    {
                                        sw.WriteLine(textNested);
                                        textNested = srNested.ReadLine();
                                    }

                                    srNested.Close();
                                    srNested.Dispose();

                                    useTemp = true;
                                }
                                else
                                {
                                    //will be error...
                                    sw.WriteLine(linetext);
                                    errorInScript = true;
                                }
                            }
                        }
                        else
                        {
                            sw.WriteLine(linetext);
                        }
                        linetext = sr.ReadLine();
                    }
                }
                catch { useTemp = false; }

                sr.Close();
                sr.Dispose();
                sw.Close();
                sw.Dispose();

                if (useTemp)
                {
                    newFile = Path.GetTempPath() + "NestedTemp.scr";
                    File.Copy(tempFile, newFile, true);

                    return true;
                }
            }
            catch { useTemp = false; }

            newFile = scriptFile;
            return false;
        }

        //Function to get active document. if no document present
        //this function will add a empty document
        private object getActiveDocument(object acadObject)
        {
            object doc = null;
            try
            {
                object AcadDocuments = acadObject.GetType().InvokeMember("Documents", BindingFlags.GetProperty, null, acadObject, null);

                int count = (int)AcadDocuments.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, AcadDocuments, null);

                //if no document present
                if (count == 0)
                {
                    AcadDocuments.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, AcadDocuments, null);

                    Thread.Sleep(1000);
                }

                doc = acadObject.GetType().InvokeMember("ActiveDocument", BindingFlags.GetProperty, null, acadObject, null);
            }
            catch
            {
            }

            return doc;
        }

        // Function to start the process..
        private bool StartBatchProcess(bool restarted, int userOption)
        {
            if (!restarted) _runOption = userOption;

            // Check if script file present
            if (!File.Exists(ScriptPath.Text))
            {
                MessageBox.Show("Please specify a valid script file.");

                ScriptPath.Focus();
                return true;
            }

            bool startBP = false;
            try
            {
                if (!restarted) Initialize_start(userOption);

                try
                {
                    Application.DoEvents();
                    this.SuspendLayout();
                }
                catch { }

                // Start the AutoCAD
                if (!startAutoCAD())
                {
                    MessageBox.Show("Unable to start AutoCAD");

                    BPbar.Visible = false;
                    label_filename.Visible = false;

                    // Enable the application start button
                    UpdateHostApplicationUI(false);

                    return false;
                }

                try
                {
                    ResumeLayout();
                }
                catch
                {
                }

                // Get active document...
                object doc = null;

                //No need to get the active document for commandline scripts
                if (!useCmdLine) doc = getActiveDocument(acadObject);

                if (!restarted)
                {
                    if (!useCmdLine)
                    {
                        object[] OnedataArray = new object[1];
                        object[] TwoVariable = new object[2];

                        OnedataArray[0] = "FILEDIA";
                        _fd = doc.GetType().InvokeMember(GetVariable, BindingFlags.InvokeMethod, null, doc, OnedataArray);

                        OnedataArray[0] = "RECOVERYMODE";
                        _rm = doc.GetType().InvokeMember(GetVariable, BindingFlags.InvokeMethod, null, doc, OnedataArray);

                        OnedataArray[0] = "LOGFILEMODE";
                        _lf = doc.GetType().InvokeMember(GetVariable, BindingFlags.InvokeMethod, null, doc, OnedataArray);

                        TwoVariable[0] = "FILEDIA";
                        TwoVariable[1] = 0;
                        doc.GetType().InvokeMember(SetVariable, BindingFlags.InvokeMethod, null, doc, TwoVariable);

                        // Set recovery mode to 0
                        TwoVariable[0] = "LOGFILEMODE";
                        TwoVariable[1] = 1;
                        doc.GetType().InvokeMember(SetVariable, BindingFlags.InvokeMethod, null, doc, TwoVariable);

                        // Set log file mode to 1
                        TwoVariable[0] = "RECOVERYMODE";
                        TwoVariable[1] = 0;
                        doc.GetType().InvokeMember(SetVariable, BindingFlags.InvokeMethod, null, doc, TwoVariable);
                    }

                    // Create the thread syn event
                    Threadinput.ThreadEvent = new AutoResetEvent(false);

                    if (!useCmdLine) _timeout.Interval = _timeoutSec * 1000 + _toolSpeed * 2;

                    pbValue = 100.0 / Threadinput._FileInfolist.Count;
                    fileCount = 0;

                    Threadinput.scriptFile = ScriptPath.Text;
                    Threadinput.startUpScript = _startUpScript;
                    Threadinput._restartDWGCount = _restartDWGCount;
                    Threadinput.logLocation = _logFilePath;
                    Threadinput.commnadLineExePath = acadExePath;
                    Threadinput.timeout = _timeoutSec;

                    Threadinput.nestedScript = checkNestedScripts(ScriptPath.Text);
                }

                // Set the new acad object....
                Threadinput.acadObject = acadObject;
                Threadinput.nCreateImage = createImage;
                Threadinput.bDiagnosticMode = diagnosticMode;

                // Start the thread again...
                batchProcessThread.RunWorkerAsync(Threadinput);

                startBP = true;
            }
            catch
            {
            }

            return startBP;
        }

        // Function to kill AutoCAD by process
        private void KillAutoCAD()
        {
            // Kill AutoCAD
            if (useCmdLine)
            {
                //not possible...
                return;
            }

            try
            {
                //Make this application as Foreground application.
                //it is noticed that, if the AutoCAD is Foreground application
                //and showing some tooltips, proc.Kill() is unable to kill the
                //AutoCAD, so as a workaround, set the ScriptPro as Foreground
                //application and kill the AutoCAD.

                Type myType = this.HostApplication.GetType();
                MethodInfo myMethodInfo = myType.GetMethod("setFocusToApplication");
                myMethodInfo.Invoke(HostApplication, null);
                Application.DoEvents();

                Process[] procs = Process.GetProcessesByName("acad");
                foreach (Process proc in procs)
                {
                    if (proc.MainWindowHandle.ToString() == _acadObjectId)
                    {
                        proc.Kill();
                        break;
                    }
                }
                acadObject = null;
            }
            catch { }
        }

        // Quits the ACAD on request
        private void quitAcad(object acadObject, bool finalQuit)
        {
            if (!useCmdLine)
            {
                try
                {
                    // First close all documents...
                    object[] TwoVariable = new object[2];

                    // Get documents...
                    object AcadDocuments = acadObject
                        .GetType()
                        .InvokeMember("Documents", BindingFlags.GetProperty, null, acadObject, null);

                    if (finalQuit)
                    {
                        // Add a new document if it is
                        //ending AutoCAD
                        AcadDocuments.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, AcadDocuments, null);
                    }

                    int count = (int)AcadDocuments.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, AcadDocuments, null);

                    // Set the system variable back
                    if (finalQuit)
                    {
                        // Reset the variables
                        object doc = acadObject.GetType().InvokeMember("ActiveDocument", BindingFlags.GetProperty, null, acadObject, null);

                        TwoVariable[0] = "FILEDIA";
                        TwoVariable[1] = _fd;
                        doc.GetType().InvokeMember(SetVariable, BindingFlags.InvokeMethod, null, doc, TwoVariable);

                        TwoVariable[0] = "RECOVERYMODE";
                        TwoVariable[1] = _rm;
                        doc.GetType().InvokeMember(SetVariable, BindingFlags.InvokeMethod, null, doc, TwoVariable);

                        // LOGFILEMODE
                        TwoVariable[0] = "LOGFILEMODE";
                        TwoVariable[1] = _lf;
                        doc.GetType().InvokeMember(SetVariable, BindingFlags.InvokeMethod, null, doc, TwoVariable);
                    }

                    while (count > 0)
                    {
                        // Reset the variables....
                        object doc = acadObject.GetType().InvokeMember("ActiveDocument", BindingFlags.GetProperty, null, acadObject, null);

                        // Close the drawing file - no need to save
                        // if required script file will have save...
                        TwoVariable[0] = false;
                        TwoVariable[1] = "";
                        doc.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, doc, TwoVariable);

                        count--;
                    }

                    acadObject.GetType().InvokeMember("Quit", BindingFlags.InvokeMethod, null, acadObject, null);
                }
                catch
                {
                    KillAutoCAD();
                }
            }

            if (!finalQuit) return;

            if (_projectName.Length != 0 && _runOption == RUN_CHECKED)
            {
                try
                {
                    bool isFailed = false;
                    foreach (ListViewItem item in DwgList.Items)
                    {
                        TagData data = (TagData)item.Tag;

                        if (data.status == false)
                        {
                            isFailed = true;
                            break;
                        }
                    }

                    if (isFailed)
                    {
                        string name = Path.GetFileNameWithoutExtension(_projectName);
                        string path = Path.GetDirectoryName(_projectName);

                        string day = DateTime.Now.Day.ToString();
                        string hour = DateTime.Now.Hour.ToString();
                        string min = DateTime.Now.Minute.ToString();
                        string sec = DateTime.Now.Second.ToString();

                        string strProject = path + "\\" + name + "_" + day + "_" + hour + "_" +
                         min + "_" + sec + "_" + "failed.bpl";

                        writeDWGList(strProject, true);

                        //creation fails, then create in temp directory...
                        if (File.Exists(strProject) == false)
                        {
                            path = Path.GetTempPath();
                            strProject = path + "\\" + name + "_" + day + "_" + hour + "_" +
                              min + "_" + sec + "_" + "failed.bpl";

                            writeDWGList(strProject, true);
                        }
                    }
                }
                catch
                {
                }
            }

            if (silentExit)
            {
                //no log file showing
            }
            else
            {
                DialogResult result = MessageBox.Show("Do you wish to view the log file?", "ScriptPro", MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes) return;

                // Show the log file
                if (bplog == null) return;

                if (File.Exists(bplog.getDetailLogFileName()))
                {
                    Process notePad = new Process();
                    notePad.StartInfo.FileName = "notepad.exe";
                    notePad.StartInfo.Arguments = bplog.getDetailLogFileName();
                    notePad.Start();
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetForegroundWindow();

        // Helper function for creating the screen dump
        // this function makes AutoCAD topmost and gets its size
        private void makeACADTopApplication(ref int left, ref int top, ref int width, ref int height)
        {
            if (useCmdLine)
            {
                //not possible...
                return;
            }

            try
            {
                object acadHwnd = acadObject.GetType().InvokeMember("HWND", BindingFlags.GetProperty, null, acadObject, null);

                int foreground = GetForegroundWindow();

                if (string.Compare(foreground.ToString(), acadHwnd.ToString(), false) != 0)
                {
                    object WindowState = acadObject.GetType().InvokeMember("WindowState", BindingFlags.GetProperty, null, acadObject, null);

                    string strWindowState = WindowState.ToString();
                    object[] OnedataArry = new object[1];

                    // Minimized
                    if (string.Compare(strWindowState, "2", false) == 0)
                    {
                        OnedataArry[0] = 1;
                        acadObject.GetType().InvokeMember("WindowState", BindingFlags.SetProperty, null, acadObject, OnedataArry);

                        Thread.Sleep(1000);
                    }
                    else
                    {
                        //minimise & maximise
                        OnedataArry[0] = 2;
                        acadObject.GetType().InvokeMember("WindowState", BindingFlags.SetProperty, null, acadObject, OnedataArry);

                        Thread.Sleep(1000);

                        OnedataArry[0] = 1;
                        acadObject.GetType().InvokeMember("WindowState", BindingFlags.SetProperty, null, acadObject, OnedataArry);

                        Thread.Sleep(1000);
                    }
                }

                left = (int)acadObject.GetType().InvokeMember("WindowLeft", BindingFlags.GetProperty, null, acadObject, null);

                top = (int)acadObject.GetType().InvokeMember("WindowTop", BindingFlags.GetProperty, null, acadObject, null);

                width = (int)acadObject.GetType().InvokeMember("Width", BindingFlags.GetProperty, null, acadObject, null);

                height = (int)acadObject.GetType().InvokeMember("Height", BindingFlags.GetProperty, null, acadObject, null);
            }
            catch { }
        }

        // Function to capture the AutoCAD screen image
        private void captureScreen(string filename, string saveLocation, ref int left, ref int top, ref int width, ref int height)
        {
            if (useCmdLine)
            {
                //not possible...
                return;
            }

            if (filename.Length == 0)
                return;

            try
            {
                Graphics myGraphics = this.CreateGraphics();
                Size s = new Size(width, height);
                Bitmap image = new Bitmap(width, height, myGraphics);
                Graphics gfx = Graphics.FromImage(image);
                gfx.CopyFromScreen(left, top, 0, 0, s);

                string imagePath = Path.GetFileName(filename);
                imagePath = Path.GetFileNameWithoutExtension(imagePath);
                imagePath = saveLocation + "\\" + imagePath + ".jpg";

                image.Save(imagePath);
                image.Dispose();
            }
            catch { }
        }

        // To check if AutoCAD is is busy or not...
        private bool IsAcadQuiescent(int tries, int sleep)
        {
            if (useCmdLine)
            {
                //not possible...
                return true;
            }
            bool ret = false;
            Thread.Sleep(sleep);

            int number = tries;

            if (tries == -1)
                number = 5;

            while (number > 0)
            {
                try
                {
                    object state = acadObject.GetType().InvokeMember("GetAcadState", BindingFlags.InvokeMethod, null, acadObject, null);

                    object quiescent = state.GetType().InvokeMember("IsQuiescent", BindingFlags.GetProperty, null, state, null);

                    ret = (bool)quiescent;

                    if (ret) break;
                    else Thread.Sleep(sleep); // 1 sec

                    if (tries != -1) number = number - 1;

                    if (acadObject == null)
                    {
                        ret = false;
                        break;
                    }
                }
                catch
                {
                    Thread.Sleep(sleep); // 1 secs
                    number = number - 1;

                    if (acadObject == null)
                    {
                        ret = false;
                        break;
                    }
                    else
                    {
                        getActiveDocument(acadObject);
                    }
                }
            }
            return ret;
        }

        #endregion AutoCAD_related Members

        //Main function starts the provided Acad application passing drawing file amd script as argumnets.
        private bool RunScriptAsCommandlineArgument(string ApplicationPath, string drawingFilePath, string scriptFilePath, int maxWaitInMilliSeconds, ref string commandline)
        {
            bool bDone = true;
            commandline = " Error while reading log file for " + drawingFilePath + "\n";
            try
            {
                string ApplicationArguments = "";

                //check if user selected application is AutoCAD or headleass AutoCAD
                bool bHeadlessAcad = isHeadlessAcad(ApplicationPath);

                if (bHeadlessAcad)
                {
                    // Kill the console.exe if it is already running...
                    Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ApplicationPath));
                    foreach (Process proc in processes)
                    {
                        proc.Kill();
                    }

                    ApplicationArguments = $"/i \"{drawingFilePath}\" /s \"{scriptFilePath}\" /l en-US";
                }
                else
                {
                    //add quit at the end of script file...
                    //as we need to quit the AutoCAD after processing the script
                    string newFile = Path.GetTempPath() + "commandline.scr";

                    StreamReader sr = new StreamReader(scriptFilePath);
                    string str = sr.ReadToEnd();
                    str = str.ToLower();
                    sr.Close();
                    sr.Dispose();
                    //add quit...
                    //str = str + "_quit\n" + "_yes\n";
                    str = $"{str}_quit{Environment.NewLine}_yes{Environment.NewLine}";

                    StreamWriter sw = new StreamWriter(newFile, false);
                    sw.Write(str);
                    sw.Close();
                    sw.Dispose();

                    ApplicationArguments = $"/i \"{drawingFilePath}\" /b \"{newFile}\"";
                }

                //start the process... No ActiveX API
                Process ProcessObj = new Process();
                ProcessObj.StartInfo.FileName = ApplicationPath;
                ProcessObj.StartInfo.Arguments = ApplicationArguments;
                ProcessObj.StartInfo.UseShellExecute = false;
                ProcessObj.StartInfo.CreateNoWindow = true;
                ProcessObj.StartInfo.RedirectStandardOutput = true;

                // The standard output buffer becomes full and so the processes blocks and never ends. This code fixes that
                // from: http://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
                StringBuilder output = new StringBuilder();
                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                {
                    ProcessObj.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null) outputWaitHandle.Set();
                        else output.AppendLine(e.Data);
                    };
                    ProcessObj.Start();
                    ProcessObj.BeginOutputReadLine();

                    if (ProcessObj.WaitForExit(maxWaitInMilliSeconds) && outputWaitHandle.WaitOne(maxWaitInMilliSeconds))
                    {
                        // success
                        output.Replace("\0", "");       // output has \0 characters between each normal character - replace them
                    }
                    else
                    {
                        // timeout
                    }

                    ProcessObj.CancelOutputRead();
                }

                //sleep for 2 second
                Thread.Sleep(2000);

                try
                {
                    //Read the commandline log for headless exe for logging purpose
                    Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ApplicationPath));
                    if (processes.Length != 0)
                    {
                        //kill the applications
                        foreach (Process proc in processes)
                        {
                            //process still alive
                            if (proc.Id != ProcessObj.Id) continue;

                            //failed...
                            bDone = false;
                            proc.Kill();
                        }
                    }
                    else if (bHeadlessAcad) commandline = output.ToString();
                }
                catch
                {
                    bDone = false;
                }

                return bDone;
            }
            catch
            {
                bDone = false;
            }

            return bDone;
        }

        //Main function which goes through the file list and run the script on each file,
        //called from batchProcessThread_DoWork
        private void batchProcessThread_DoWork_CommandlineArgument(ref ThreadInput input, ref BackgroundWorker worker)
        {
            foreach (FileInfo info in input._FileInfolist)
            {
                int reportStatus = 0;

                try
                {
                    // Report start of the process...
                    worker.ReportProgress(OPEN_NEW_DWG, info);

                    // Wait till timer is started
                    Threadinput.ThreadEvent.WaitOne();

                    // Check for cancelation..
                    if (batchProcessThread.CancellationPending)
                        break;

                    Thread.Sleep(100);

                    string scriptFile = input.scriptFile;
                    bool errorInScript = false;

                    if (input.nestedScript != 0)
                    {
                        string strOldScr = input.scriptFile;

                        if (!replaceKeyWords(strOldScr, ref scriptFile, info._fileName))
                        {
                            //No Keywords, so set back the file name
                            scriptFile = input.scriptFile;
                        }
                    }
                    if (input.nestedScript == 2) //nested
                    {
                        string strOldScr = scriptFile;
                        string scriptPath = Path.GetDirectoryName(input.scriptFile);
                        while (processNestedScripts(scriptPath, strOldScr, ref scriptFile, ref errorInScript))
                        {
                            strOldScr = scriptFile;

                            //nested scripts may add key words
                            if (replaceKeyWords(strOldScr, ref scriptFile, info._fileName))
                            {
                                strOldScr = scriptFile;
                            }
                        }
                    }

                    //run the script on AutoCAD/headless AutoCAD
                    if (RunScriptAsCommandlineArgument(input.commnadLineExePath, info._fileName, scriptFile, input.timeout * 1000, ref info._logFile))
                        reportStatus = CLOSE_DWG_SUCCESS; //done
                    else
                        reportStatus = CLOSE_DWG_FAILED;//fail
                }
                catch
                {
                    reportStatus = CLOSE_DWG_FAILED; //fail
                }

                worker.ReportProgress(reportStatus, info);

                Threadinput.ThreadEvent.WaitOne();

                if (batchProcessThread.CancellationPending)
                    break;
            }
        }

        // Functions related to batch process thread

        // Main function for batch process...
        // Opens each drawing file and runs the selected script...
        private void batchProcessThread_DoWork(
          object sender, DoWorkEventArgs e
        )
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            ThreadInput input = (ThreadInput)e.Argument;

            //for commandline argument call different function
            if (useCmdLine)
            {
                batchProcessThread_DoWork_CommandlineArgument(ref input, ref worker);
                e.Result = true;
                return;
            }

            object[] OnedataArray = new object[1];
            object[] ThreeVariable = new object[3];
            object[] TwoVariable = new object[2];

            object acadObject = input.acadObject;

            //Version 2.1
            int getActiveDoc = 5;
            object AcadDocuments = null;
            object ActiveDocument = null;

            while (getActiveDoc > 0)
            {
                // Get the AcadDocuments
                try
                {
                    AcadDocuments =
                      acadObject.GetType().InvokeMember(
                        "Documents",
                        BindingFlags.GetProperty,
                        null, acadObject, null
                      );

                    ActiveDocument =
                      acadObject.GetType().InvokeMember(
                        "ActiveDocument",
                        BindingFlags.GetProperty,
                        null, acadObject, null
                      );

                    break;
                }
                catch
                {
                    getActiveDoc--;

                    //sleep for 1 second
                    Thread.Sleep(1000);
                }
            }

            bool isAcadKilled = false;
            int reportStatus = 0;

            // Run the Threadinput.startUpScript...
            if (Threadinput.startUpScript.Length != 0)
            {
                CheckFileDiaSystemVariable(ActiveDocument);

                OnedataArray[0] =
                  "_.SCRIPT " + Threadinput.startUpScript + "\n";
                ActiveDocument.GetType().InvokeMember(
                  "SendCommand",
                  BindingFlags.InvokeMethod,
                  null, ActiveDocument, OnedataArray
                );
            }

            // 5 seconds so that AutoCAD is ready
            IsAcadQuiescent(5, 1000);

            // Call Open
            int fileCount = 0;
            int DWGsprocessed = 0;
            bool returnResult = true;

            foreach (FileInfo info in input._FileInfolist)
            {
                fileCount++;

                if (info._processed)
                    continue;

                isAcadKilled = false;
                _imageCreated = false;
                try
                {
                    // Report start of the process...
                    worker.ReportProgress(OPEN_NEW_DWG, info);

                    // Wait till timer is started
                    Threadinput.ThreadEvent.WaitOne();

                    // Check for cancelation..
                    if (batchProcessThread.CancellationPending)
                        break;

                    Thread.Sleep(100);

                    if (Threadinput.nCreateImage != 2) // No image capture
                        makeACADTopApplication(
                          ref _left, ref _top, ref _width, ref _height
                        );

                    //version 2.1
                    Thread.Sleep(500 + _toolSpeed); //100

                    //do not open the document, if user wants to run
                    //the script on dummy/empty document
                    if (!runWithoutOpen)
                    {
                        ThreeVariable[0] = info._fileName;//Name
                        ThreeVariable[1] = false; //ReadOnly
                        ThreeVariable[2] = " "; //Password
                        ActiveDocument =
                        AcadDocuments.GetType().InvokeMember(
                         "Open",
                         BindingFlags.InvokeMethod,
                         null, AcadDocuments, ThreeVariable
                       );
                    }
                    else
                    {
                        ActiveDocument = getActiveDocument(acadObject);
                    }

                    //add on 4-1-2011 - for ACA testing....
                    Thread.Sleep(500 + _toolSpeed); //100
                                                    //version 2.1
                                                    // DiagnosticMode, now show a message box
                                                    // may be better UI later....

                    if (Threadinput.bDiagnosticMode)
                    {
                        // Show the message box and hold the screen...
                        MessageBox.Show(
                          "Press OK to continue...",
                          "Diagnostic mode", MessageBoxButtons.OK
                        );
                    }

                    // Read the system variable LOGFILENAME
                    OnedataArray[0] = "LOGFILENAME";
                    info._logFile =
                      (string)ActiveDocument.GetType().InvokeMember(
                      GetVariable,
                      BindingFlags.InvokeMethod,
                      null, ActiveDocument, OnedataArray
                    );

                    // Sleep for 0.5 seconds...
                    Thread.Sleep(500 + _toolSpeed);
                    CheckFileDiaSystemVariable(ActiveDocument);

                    //1.0.2
                    string scriptFile = input.scriptFile;
                    bool errorInScript = false;

                    if (input.nestedScript != 0)
                    {
                        string strOldScr = input.scriptFile;

                        if (!replaceKeyWords(strOldScr, ref scriptFile, info._fileName))
                        {
                            //No Keywords, so set back the file name
                            scriptFile = input.scriptFile;
                        }
                    }
                    if (input.nestedScript == 2) //nested
                    {
                        string strOldScr = scriptFile;
                        string scriptPath = Path.GetDirectoryName(input.scriptFile);
                        while (processNestedScripts(scriptPath, strOldScr, ref scriptFile, ref errorInScript))
                        {
                            strOldScr = scriptFile;

                            //nested scripts may add key words
                            if (replaceKeyWords(strOldScr, ref scriptFile, info._fileName))
                            {
                                strOldScr = scriptFile;
                            }
                        }
                    }

                    OnedataArray[0] = "_.SCRIPT " + scriptFile /*input.scriptFile*/ + "\n";
                    ActiveDocument.GetType().InvokeMember(
                      "SendCommand",
                      BindingFlags.InvokeMethod,
                      null, ActiveDocument, OnedataArray
                    );

                    IsAcadQuiescent(-1, 1000);

                    if (Threadinput.nCreateImage == 0)
                    {
                        _imageCreated = true;
                        captureScreen(
                          info._fileName, input.logLocation,
                          ref _left, ref _top, ref _width, ref _height
                        );
                    }

                    // DiagnosticMode, now show a message box
                    // may be better UI later....
                    if (Threadinput.bDiagnosticMode)
                    {
                        // Show the message box and hold the screen...
                        MessageBox.Show(
                          "Press OK to continue...",
                          "Diagnostic mode", MessageBoxButtons.OK
                        );
                    }

                    // Close the drawing file - no need to save
                    // if required script file will have save...
                    if (!runWithoutOpen)
                    {
                        TwoVariable[0] = false;
                        TwoVariable[1] = "";
                        ActiveDocument.GetType().InvokeMember(
                          "Close",
                          BindingFlags.InvokeMethod,
                          null, ActiveDocument, TwoVariable
                        );
                    }

                    Thread.Sleep(500);

                    if (!errorInScript)
                        reportStatus = CLOSE_DWG_SUCCESS; //done
                    else
                        reportStatus = CLOSE_DWG_FAILED; //failed
                }
                catch
                {
                    //failed
                    if (!_imageCreated && Threadinput.nCreateImage == 1)
                    {
                        captureScreen(
                          info._fileName, input.logLocation,
                          ref _left, ref _top, ref _width, ref _height
                        );
                    }
                    // Either fail to open drawing
                    // AutoCAD killed...
                    // AutoCAD crashed...
                    reportStatus = CLOSE_DWG_FAILED; //failed
                    isAcadKilled = true;
                }

                DWGsprocessed++;
                worker.ReportProgress(reportStatus, info);
                Threadinput.ThreadEvent.WaitOne();

                // AutoCAD is either killed, crashed OR busy....
                if (isAcadKilled)
                {
                    Process[] procs = Process.GetProcessesByName("acad");
                    foreach (Process proc in procs)
                    {
                        if (string.Compare(proc.MainWindowHandle.ToString(),
                       _acadObjectId, false) == 0)
                        {
                            // AutoCAD is showing some dialog
                            // or is not responding...
                            isAcadKilled = false;
                            returnResult = true;
                            break;
                        }
                    }
                }
                // If AutoCAD is killed, then break
                if (isAcadKilled)
                    break;

                // Check for cancellation..
                if (batchProcessThread.CancellationPending)
                    break;

                // DWG
                if (DWGsprocessed == Threadinput._restartDWGCount)
                {
                    isAcadKilled = false;
                    returnResult = false;
                    break;
                }
            }

            // Check AutoCAD...
            if (!isAcadKilled)
            {
                e.Result = returnResult;
            }
            else
            {
                // AutoCAD is no more so return always false
                e.Result = false;
            }
        }

        //to check the file dia system variable
        private void CheckFileDiaSystemVariable(object ActiveDocument)
        {
            try
            {
                object[] OnedataArray = new object[1];
                OnedataArray[0] = "FILEDIA";
                short fd =
                  (short)ActiveDocument.GetType().InvokeMember(
                    GetVariable,
                    BindingFlags.InvokeMethod,
                    null, ActiveDocument, OnedataArray
                  );

                if (fd == 1)
                {
                    //reset the variable
                    _fd = fd;

                    object[] TwoVariable = new object[2];
                    TwoVariable[0] = "FILEDIA";
                    TwoVariable[1] = 0;
                    ActiveDocument.GetType().InvokeMember(
                      SetVariable,
                      BindingFlags.InvokeMethod,
                      null, ActiveDocument, TwoVariable
                    );
                }
            }
            catch { }
        }

        // Batch process end call back...
        // Called in main thread
        private void batchProcessThread_RunWorkerCompleted(
          object sender, RunWorkerCompletedEventArgs e
        )
        {
            bool batchProcessCompleted = true;
            try
            {
                batchProcessCompleted = (bool)e.Result;
            }
            catch { }

            bool finalQuit = false;

            if (batchProcessCompleted)
            {
                finalQuit = true;
            }
            else
            {
                if (_stopBatchProcess)
                    finalQuit = true;
            }

            if (acadObject != null)
            {
                quitAcad(acadObject, finalQuit);

                acadObject = null;
            }
            else
            {
                if (finalQuit)
                {
                    // AutoCAD is no more... but we need to set
                    // a few settings like filedia, etc.
                    // So restart AutoCAD...
                    startAutoCAD();

                    // Set them and quit AutoCAD....
                    quitAcad(acadObject, finalQuit);
                }
            }

            if (batchProcessCompleted)
            {
                BPbar.Visible = false;
                label_filename.Visible = false;

                //Enable the application start button
                UpdateHostApplicationUI(false);

                //if user wants the exit of ScriptPro, then perform
                if (silentExit)
                {
                    //exit the application
                    Type myType = this.HostApplication.GetType();
                    MethodInfo myMethodInfo = myType.GetMethod("exitApplication");
                    myMethodInfo.Invoke(HostApplication, null);
                }
            }
            else
            {
                while (batchProcessThread.IsBusy)
                {
                    // Wait till back ground thread comes out....
                }

                if (!_stopBatchProcess)
                {
                    // Restart the Batch Process.
                    // _stopBatchProcess = false;
                    StartBatchProcess(true, _runOption);
                }
                else
                {
                    BPbar.Visible = false;
                    label_filename.Visible = false;

                    // Enable the application start button
                    UpdateHostApplicationUI(false);
                }

                _stopBatchProcess = false;
            }
        }

        // Batch process threads process update call back
        // Called in main thread, we mainly update the UI

        private void batchProcessThread_ProgressChanged(
          object sender, ProgressChangedEventArgs e
        )
        {
            try
            {
                // Start of a new drawing
                if (e.ProgressPercentage == OPEN_NEW_DWG)
                {
                    // Start the timer
                    if (!useCmdLine)
                        _timeout.Start();

                    try
                    {
                        FileInfo info = (FileInfo)e.UserState;

                        _currentDWG = info._fileName;

                        int fileNumber = fileCount + 1;
                        label_filename.Text =
                            fileNumber.ToString() + " / " +
                            Threadinput._FileInfolist.Count.ToString();
                        label_filename.Invalidate();
                        info._logFile = "";

                        ListViewItem item = DwgList.Items[info._index];
                        item.EnsureVisible();
                        itemColor = item.BackColor;
                        item.BackColor = Color.Gold;
                    }
                    catch { }
                }
                else
                {
                    // Stop the timer...
                    // End of drawing
                    if (!useCmdLine)
                        _timeout.Stop();

                    // Update the process bar
                    try
                    {
                        fileCount = fileCount + 1;
                        double dValue = pbValue * fileCount;

                        if (dValue > 100.0)
                            dValue = 100.0;

                        BPbar.Value = (int)dValue;
                    }
                    catch { }

                    // Get the file info
                    FileInfo info = (FileInfo)e.UserState;

                    // Get the log file details

                    // Find the file
                    ListViewItem item = DwgList.Items[info._index];

                    item.BackColor = itemColor;

                    // Get the file name
                    TagData data = (TagData)item.Tag;

                    if (e.ProgressPercentage == CLOSE_DWG_SUCCESS)
                    {
                        item.SubItems[2].Text = "Done";
                        info._status = true;
                        data.status = true;
                    }
                    else if (e.ProgressPercentage == CLOSE_DWG_FAILED)
                    {
                        item.SubItems[2].Text = "Failed";
                        item.ForeColor = Color.Red;
                        info._status = false;
                        data.status = false;
                    }
                    info._timeDate = DateTime.Now.ToString();
                    info._processed = true;

                    if (bplog != null)
                    {
                        string acadLog = "";

                        if (useCmdLine)
                        {
                            //log
                            acadLog = info._logFile;
                            if (acadLog.Length == 0)
                            {
                                acadLog =
                                  "No detail log when running script as commandline argument for AutoCAD";
                            }

                            // Log the details
                            acadLog = acadLog.Replace("\b", "");

                            bplog.Log(
                              data.DwgName, acadLog, _projectName, data.status
                            );
                        }
                        else
                        {
                            if (!runWithoutOpen)
                            {
                                if (File.Exists(info._logFile))
                                {
                                    //
                                    try
                                    {
                                        StreamReader SR = File.OpenText(info._logFile);
                                        acadLog = SR.ReadToEnd();
                                        SR.Close();
                                    }
                                    catch { }
                                }

                                if (acadLog.Length == 0)
                                {
                                    acadLog =
                                      "Error while reading log file for " +
                                      info._fileName + "\n";
                                }

                                // Log the details
                                bplog.Log(
                                  data.DwgName, acadLog, _projectName, data.status
                                );
                            }
                            else
                            {
                                acadLog =
                                  "No detail log when running script without opening drawing file" +
                                           "\n";
                                bplog.Log(
                             data.DwgName, acadLog, _projectName, data.status
                           );
                            }
                        }
                    }
                }
                if (_stopBatchProcess)
                    batchProcessThread.CancelAsync();

                // Release the waiting thread

                Threadinput.ThreadEvent.Set();
            }
            catch { }
        }

        // Timer functions...

        // Timer elapsed function

        private void _timeout_Elapsed(
          object sender, System.Timers.ElapsedEventArgs e
        )
        {
            //no work if commandline working
            if (useCmdLine)
                return;

            try
            {
                if (batchProcessThread.IsBusy)
                {
                    if (!diagnosticMode)
                    {
                        _timeout.Stop();
                        _killAcad = true;
                    }
                }
                else
                {
                    batchProcessThread.CancelAsync();
                }
            }
            catch { }
        }

        // Timeout thread main function
        private void _timeoutWk_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker timer = sender as BackgroundWorker;

            while (!timer.CancellationPending) //infinite loop
            {
                Thread.Sleep(2000);//sleep 2 second

                // change on 20-10-2010...
                if (DrawingListControl._killAcad)
                    timer.ReportProgress(0, null);
            }
        }

        // Function which is called when time out occurs
        // This function is kill from Main thread, so you
        // kill AutoCAD here
        private void _timeoutWk_ProgressChanged(
          object sender, ProgressChangedEventArgs e
        )
        {
            if (_killAcad)
            {
                // Take the screenshot if required
                try
                {
                    if (!_imageCreated && createImage == 1)
                    {
                        _imageCreated = true;
                        captureScreen(
                          _currentDWG, _logFilePath,
                          ref _left, ref _top, ref _width, ref _height
                        );
                    }
                }
                catch { }

                KillAutoCAD();
                _killAcad = false;
            }
        }

        // Function which updates the host application.
        // late binding is used... just in case we use any other
        // host application
        private void UpdateHostApplicationUI(bool processStarted)
        {
            try
            {
                Type myType = this.HostApplication.GetType();
                MethodInfo myMethodInfo = myType.GetMethod("processStatus");

                ParameterInfo[] myParameters = myMethodInfo.GetParameters();
                object[] OnedataArray = new object[1];
                OnedataArray[0] = processStarted;
                myMethodInfo.Invoke(HostApplication, OnedataArray);

                _isProcessRunning = processStarted;
            }
            catch { }
        }

        private void DrawingListControl_Load(object sender, EventArgs e)
        {
        }

        public void wizardDWGList()
        {
            //Wizard myWizard = new Wizard();
            //myWizard.prepareForStep1();
            WizardForm myWizard = new WizardForm();

            if (myWizard.ShowDialog() == DialogResult.OK)
            {
                newDWGList();

                ScriptPath.Text = myWizard.scriptPath;
                acadExePath = myWizard.acadPath;

                useCmdLine = isHeadlessAcad(acadExePath);

                foreach (string dwgName in myWizard.dwgList)
                {
                    AddDWGtoView(dwgName, true);
                }

                _scriptPath = ScriptPath.Text;

                runSelectedExe = false;

                if (acadExePath.Length != 0)
                    runSelectedExe = true;

                if (myWizard.startScriptPro)
                {
                    runCheckedFiles();
                }
            }
        }

        public void hideControlsForWizard()
        {
            this.Controls.Remove(label_filename);
            this.Controls.Remove(BPbar);
            this.Controls.Remove(scriptGBox);

            wizardMode = true;
            DwgList.CheckBoxes = false;
            DwgList.Dock = DockStyle.Fill;

            DwgList.Columns.RemoveAt(2);

            int width = DwgList.Width;

            DwgList.Columns[0].Width = (int)(width * 0.25);

            // Path
            DwgList.Columns[1].Width = (int)(width * 0.65);
        }

        public void populateDWGlist(List<string> list)
        {
            foreach (ListViewItem item in DwgList.Items)
            {
                TagData data = (TagData)item.Tag;
                list.Add(data.DwgName);
            }
        }
    }

    internal class FileInfo
    {
        public FileInfo()
        {
            _index = -1;
            _processed = false;
            _logFile = "";
        }

        public string _fileName;
        public string _timeDate;
        public string _logFile;
        public bool _status;
        public int _index;
        public bool _processed;
    }

    internal class ThreadInput
    {
        public ThreadInput()
        {
            _FileInfolist = new List<FileInfo>();
            ThreadEvent = null;

            nCreateImage = 2;
            bDiagnosticMode = true;
            nestedScript = 0;
        }

        public object acadObject;
        public List<FileInfo> _FileInfolist;
        public string scriptFile;
        public string startUpScript;
        public int _restartDWGCount;
        public AutoResetEvent ThreadEvent;
        public string logLocation;
        public int nCreateImage;
        public bool bDiagnosticMode;
        public string commnadLineExePath = "";
        public int timeout;

        //holds
        //1 - when key words are used
        //2 - when nesting script
        //0 - for no keyword or nested...
        public int nestedScript;
    }

    internal class TagData
    {
        public TagData()
        {
            _status = true;
        }

        private string _dwgName;
        private bool _status;

        public string DwgName
        {
            set { _dwgName = value; }
            get { return _dwgName; }
        }

        public bool status
        {
            set { _status = value; }
            get { return _status; }
        }
    }

    public class ReportLog
    {
        private const string logExt = "log";
        private string _logFile;
        private string _logDetailFile;

        public ReportLog()
        {
        }

        public ReportLog(string pathName, string projectName)
        {
            string day = DateTime.Now.Day.ToString();
            string hour = DateTime.Now.Hour.ToString();
            string min = DateTime.Now.Minute.ToString();
            string sec = DateTime.Now.Second.ToString();

            string name = "SPlog";

            if (projectName.Length != 0)
                name = Path.GetFileNameWithoutExtension(projectName);

            _logFile =
              pathName + "\\" + name + "_" + day + "_" + hour + "_" +
              min + "_" + sec + "." + logExt;

            StreamWriter sw =
              new StreamWriter(_logFile, true);
            sw.Flush();
            sw.Close();

            _logDetailFile =
             pathName + "\\" + name + "_Detail_" + day + "_" + hour + "_" +
             min + "_" + sec + "." + logExt;

            sw = new StreamWriter(_logDetailFile, true);

            sw.WriteLine("");
            sw.WriteLine("");
            sw.Flush();
            sw.Close();
        }

        public string getLogFileName()
        {
            return _logFile;
        }

        public string getDetailLogFileName()
        {
            return _logDetailFile;
        }

        public void setLogFileName(string fileName)
        {
            _logFile = fileName;
        }

        public void Log(string filename, string strAcadLog,
                string strProject, bool bResult)
        {
            try
            {
                string logMsg;

                if (bResult)
                    logMsg = "Done";
                else
                    logMsg = "Failed";

                StreamWriter sw = new StreamWriter(_logFile, true);
                sw.WriteLine(filename + "," + logMsg);
                sw.Flush();
                sw.Close();

                sw = new StreamWriter(_logDetailFile, true);
                sw.WriteLine("------------------------------------------------------------------------------");
                sw.WriteLine("------------------------------------------------------------------------------");

                sw.WriteLine("Project file: 	" + strProject);
                sw.WriteLine("Drawing file: 	" + filename);
                sw.WriteLine("");
                sw.WriteLine("Processed by Computer: 	"
                    + System.Environment.MachineName);
                sw.WriteLine("");
                sw.WriteLine("User name   : 	"
                    + System.Environment.UserName);
                sw.WriteLine("");

                sw.WriteLine("[ Status summary ]");
                sw.WriteLine(logMsg);
                sw.WriteLine("");//empty line
                sw.WriteLine("");

                sw.WriteLine(strAcadLog);
                sw.Flush();
                sw.Close();
            }
            catch { }
        }
    }
}