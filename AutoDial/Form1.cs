﻿using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tesseract;
using Tesseract.ConsoleDemo;
using System.Configuration;

namespace AutoDial
{
    public partial class Form1 : Form
    {
        private static Logger m_logger;
        private LogAndErrorsUtils m_logAndErr;
        private ImageManipulationUtils m_imgManUtils;
        /// <summary>
        /// Keep track if we have sent an error popup notification, if so use this value to supress future popups, under the assumption that the first one is the most important and probably the originator of the future popups.
        /// </summary>
       
        public Form1()
        {
            //Setup the Logging system
            m_logAndErr = new LogAndErrorsUtils(AutoDialIcon, ToolTipIcon.Error);
            m_imgManUtils = new ImageManipulationUtils(m_logAndErr);
            m_logger = m_logAndErr.getLogger();
           
            //Initialise the Form
            InitializeComponent();

            m_logger.Info("###################################################################");
            m_logger.Info("Starting Program");

            this.registerHotkey();

            this.customHide();
        }

        /// <summary>
        /// This will be the main logic of the Program
        /// </summary>
        private void ExecuteProgram()
        {
            m_logger.Info("Start: ExecuteProgram()");

            string strTargetProcessName = System.Configuration.ConfigurationManager.AppSettings["targetProcessName"];
            if (strTargetProcessName != null)
            {
                m_logger.Debug("Target process read from configuration: " + strTargetProcessName);
                string dateStamp = "";
                Bitmap capturedImg = this.CaptureApplication(strTargetProcessName, ref dateStamp);

                if (capturedImg == null)
                {
                    m_logAndErr.setFirstErrorSignalled(false);
                    return;
                }
                
                m_imgManUtils.saveGrayScaleImg(ref capturedImg, dateStamp);
                
                string rawText = this.ScanImage(dateStamp);
                string phoneNumber = this.extractPhoneNumber(rawText);
                phoneNumber = this.cleanNumber(phoneNumber);
                this.callNumber(phoneNumber);
            }
            else
            {
                m_logger.Error("Target process not found in config, please check configuration file.");
                this.errorPopup("Target process not found in config", "Target process not found in config, please check configuration file.");
            }

        }

        #region Hotkeys

        /// <summary>
        // /Register the Hotkeys
        /// </summary>
        public void registerHotkey()
        {
            m_logger.Debug("Start: registerHotkey()");
            //User32.RegisterHotKey(this.Handle, 1, MOD_CONTROL, (int)Keys.F12);

            string hotKeyIDString = System.Configuration.ConfigurationManager.AppSettings["hotKeyID"];
            string hotKeyModString = System.Configuration.ConfigurationManager.AppSettings["hotKeyMod"];
            int hotKeyID = -1;
            int hotKeyMod = -1;

            if (hotKeyIDString == null)
            {
                m_logger.Error("hotKeyID not found in Config File");
            }
            else if (hotKeyModString == null)
            {
                m_logger.Error("hotKeyMod not found in Config File");
            }
            else
            {
                hotKeyID = int.Parse(hotKeyIDString);
                hotKeyMod = int.Parse(hotKeyModString);

                m_logger.Info("Setting Hotkey to: Mod:" + hotKeyMod + " ID:" + hotKeyID);
                User32.RegisterHotKey(this.Handle, 1, hotKeyMod, hotKeyID);
            }

        }

        /// <summary>
        /// Receive Hotkey events
        /// </summary>
        /// <param name="m"></param>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Hotkeys.Constants.WM_HOTKEY_MSG_ID)
            {
                if ((int)m.WParam == 1)
                {
                    this.ExecuteProgram();
                }
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// When the window is closing, Unregister the Hotkey
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_logger.Debug("FormClosing, unregestering Hotkey");
            User32.UnregisterHotKey(this.Handle, 1);

            this.cleanupFolder();
        }

        #endregion

        #region ImageManipulation

        public void RecolourImage(string dateStamp)
        {
            m_logger.Debug("Start: RecolourImage()");

            //string file = @"c:\image_2014-12-18_14-31-15.png";

            //if (System.IO.File.Exists(file))
            if (System.IO.File.Exists(this.generateNameInitial(dateStamp)))
            {
                Bitmap imageTest = (Bitmap)Image.FromFile(this.generateNameInitial(dateStamp));
                //Bitmap imageTest = (Bitmap)Image.FromFile(file);
                imageTest = this.GrayScale(imageTest);

                m_logger.Debug("Saving image: " + generateNameGrey(dateStamp));
                imageTest.Save(generateNameGrey(dateStamp));
            }
            else
            {
                m_logger.Error("RecolourImage() unable to locate file. " + this.generateNameInitial(dateStamp));
                this.errorPopup("RecolourImage() unable to locate file.", "RecolourImage() unable to locate file. " + this.generateNameInitial(dateStamp));
            }
        }

        /// <summary>
        /// Convert the provided BitMap into a Greyscale image
        /// </summary>
        /// <param name="Bmp">The Source image</param>
        /// <returns>The resulting Greyscale Image</returns>
        public Bitmap GrayScale(Bitmap Bmp)
        {

            m_logger.Debug("Start: GrayScale()");


            string captureLocationX = System.Configuration.ConfigurationManager.AppSettings["captureLocationX"];
            string captureLocationY = System.Configuration.ConfigurationManager.AppSettings["captureLocationY"];
            string captureWidth = System.Configuration.ConfigurationManager.AppSettings["captureWidth"];
            string captureHeight = System.Configuration.ConfigurationManager.AppSettings["captureHeight"];



            //int startX = 92;
            int startX = this.ConvetStringToInt("captureLocationX", captureLocationX);

            //int startY = 322;
            int startY = this.ConvetStringToInt("captureLocationY", captureLocationY);

            //int width = 500;
            int width = this.ConvetStringToInt("captureWidth", captureWidth);

            //int height = 26;
            int height = this.ConvetStringToInt("captureHeight", captureHeight);


            //int endX = width + startX;
            //int endY = height + startY;

            Bitmap OutputImage = new Bitmap(width, height);

            int rgb;
            Color c;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    c = Bmp.GetPixel(x + startX, y + startY);
                    //rgb = (int)((c.R + c.G + c.B) / 3);
                    rgb = (int)(c.R * 0.21 + c.G * 0.72 + c.B * 0.07);
                    OutputImage.SetPixel(x, y, Color.FromArgb(rgb, rgb, rgb));
                }
            return OutputImage;
        }

        public int ConvetStringToInt(string name, string source)
        {
            if (source == null)
            {
                m_logger.Error("Error: " + name + " Is Null, Check config file.");

                this.errorPopup("Config Error", "Error: " + name + " Is Null, Check config file.");

                return 0;
            }

            return Convert.ToInt32(source);
        }

        /*
         
         public Bitmap GrayScale(Bitmap Bmp)
        {
            logger.Debug("Start: GrayScale()");
            int startY = 0;
            int startX = 0;

            int width = 0;
            int height = 0;

            int rgb;
            Color c;

            for (int y = 0; y < Bmp.Height; y++)
                for (int x = 0; x < Bmp.Width; x++)
                {
                    c = Bmp.GetPixel(x, y);
                    //rgb = (int)((c.R + c.G + c.B) / 3);
                    rgb = (int)(c.R * 0.21 + c.G * 0.72 + c.B * 0.07);
                    Bmp.SetPixel(x, y, Color.FromArgb(rgb, rgb, rgb));
                }
            return Bmp;
        }
         
         */

        #endregion

        #region ScreenCapture

        /// <summary>
        /// Capture a screenshot of a specifc application
        /// </summary>
        /// <param name="processName">The process name of the target application</param>
        public Bitmap CaptureApplication(string processName, ref string dateStamp)
        {
            m_logger.Debug("Start: CaptureApplication(), searching for " + processName);

            Process[] processArray = Process.GetProcessesByName(processName);

            if (processArray.Count() == 0)
            {
                m_logger.Error("Target process not found: " + processName + Environment.NewLine + "Please check that the program is running, if so then check that the configured processName is correct");

                this.errorPopup("Error", "Target process not found: " + processName + Environment.NewLine + "Please check that the program is running, if so then check that the configured processName is correct");
                return null;
            }
            else
            {
                m_logger.Debug("Found " + processArray.Count() + " matching processes");
            }

            if (processArray.Count() > 1)
            {
                m_logger.Debug("More than one process");
                for (int i = 0; i < processArray.Count(); ++i)
                {
                    m_logger.Debug("Process id: " + processArray[i].Id);
                }
            }
            Process process = processArray[0];

            var rect = new User32.Rect();
            User32.GetWindowRect(process.MainWindowHandle, ref rect);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;

            //Create a new bitmap.
            //            Bitmap bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
            //                                           Screen.PrimaryScreen.Bounds.Height,
            //                                           PixelFormat.Format32bppArgb);

            //Create a new bitmap.
            Bitmap bmpScreenshot = new Bitmap(width,
                                           height,
                                           PixelFormat.Format32bppArgb);

            // Temporary change
            // Store the bitmap image


            // Create a graphics object from the bitmap.
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Take the screenshot from the upper left corner to the right bottom corner.
            gfxScreenshot.CopyFromScreen(rect.left,
                                        rect.top,
                                        0,
                                        0,
                                        new Size(width, height),
                                        CopyPixelOperation.SourceCopy);

            // Save the screenshot to the specified path that the user has chosen.
            dateStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            m_logger.Debug("Saving image: " + this.generateNameInitial(dateStamp));

            if (true)
            {
                bmpScreenshot.Save("" + this.generateNameInitial(dateStamp), System.Drawing.Imaging.ImageFormat.Png);
            }

            return bmpScreenshot;

            /*
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

            bmp.Save("c:\\tmp\\test.png", System.Drawing.Imaging.ImageFormat.Png);*/
        }

        #endregion

        #region ReadImage

        public string ScanImage(string dateStamp)
        {
            m_logger.Debug("Start: ScanImage()");
            string rawText = "";
            
            var imagePath = this.generateNameGrey(dateStamp);
            
            if (System.IO.File.Exists(imagePath))
            {
                // tesseract data path
                string strTesseractDataPath = "C:\\Autodial\\Dialing\\AutoDial_0.14\\tessdata";

                try
                {
                    var logger2 = new FormattedConsoleLogger();
                    var resultPrinter = new ResultPrinter(logger2);

                    
                   using (var engine = new TesseractEngine(@strTesseractDataPath, "eng", EngineMode.Default))
                    {
                        
                        m_logger.Trace("Tesseract found in {0}", strTesseractDataPath);

                        using (var img = Pix.LoadFromFile(imagePath))
                        {
                            using (logger2.Begin("Process image"))
                            {
                                using (Page page = engine.Process(img))
                                {
                                    rawText = page.GetText();

                                    m_logger.Trace(rawText);

                                    m_logger.Trace("Text: {0}", rawText);
                                    m_logger.Trace("Mean confidence: {0}", page.GetMeanConfidence());

                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_logger.Trace("Couldn't find the tesseract data on {0}", strTesseractDataPath);
                    Trace.TraceError(e.ToString());
                    Console.WriteLine("Unexpected Error: " + e.Message);
                    Console.WriteLine("Details: ");
                    Console.WriteLine(e.ToString());
                }
                return rawText;
            }
            else
            {
                m_logger.Error("ScanImage unable to locate file: " + imagePath);
                this.errorPopup("File Not Found", "ScanImage unable to locate file: " + imagePath);
                
                return null;
            }
        }

        public string extractPhoneNumber(string inputText)
        {
            m_logger.Debug("Start: extractPhoneNumber()");
           
            //Extract the PhoneNumber
            if (inputText != null)
            {
                inputText = inputText.Replace("\n", " ");
               
                Match result = Regex.Match(inputText, @"(?<=UTODIAL: )([\s0-9+-]*)", RegexOptions.Multiline);
                
                // Sometimes the ':' character is mixed with the 'Z' character. Let's test if a number is recognised this way.
                Match resultZ = Regex.Match(inputText, @"(?<=UTODIALZ )([\s0-9+-]*)", RegexOptions.Multiline);

                if (result.Success)
                {
                    m_logger.Debug("Found match:" + result.Value);

                    //string value = result.Value;
                    return cleanNumber(result.Value);
                }
                else if (resultZ.Success)
                {
                    m_logger.Debug("Found(Z) match:" + resultZ.Value);

                    return cleanNumber(resultZ.Value);
                }
                else
                {
                    m_logger.Error("Unable to find Match");
                    this.errorPopup("Cant find AUTODIAL", "extractPhoneNumber() was unable to find AUTODIAL value");
                    return null;
                }
            }
            else
            {
                m_logger.Error("extractPhoneNumber() inputText is NULL");
                this.errorPopup("Null input", "extractPhoneNumber() inputText is NULL");
                return null;
            }
        }

        public string cleanNumber(string inputNumber)
        {
            return inputNumber;
            /*
            //inputNumber = "123  4 56a";
            //Remove spaces
            inputNumber = inputNumber.Replace(" ", string.Empty);
            logger.Debug("Start: cleanNumber() initial number: " + inputNumber);
            if (inputNumber != null)
            {
                //inputNumber = inputNumber.Replace("D", "0");
                if (inputNumber.All(char.IsNumber))
                {

                }
                else
                {
                    logger.Error("Error, number contains non Digit Characters: " + inputNumber);
                    this.errorPopup("Null input", "number contains non Digit Characters: " + inputNumber);
                    return null;
                }
                
                logger.Debug("Returning:" + inputNumber);
                return inputNumber;
            }
            else
            {
                logger.Error("Error, provided number is Null");
                this.errorPopup("Null input", "cleanNumber() inputText is NULL");
                return null;
            }
            */
        }

        #endregion

        #region Input Handeling

        private void executeProgramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_logger.Debug("Executing program from menu");
            this.ExecuteProgram();
        }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_logger.Debug("Exiting Application from Menu");
            Application.Exit();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)//this code gets fired on every resize
            {                                                                                      //so we check if the form was minimized
                Hide();//hides the program on the taskbar
            }
        }

        private void showHideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_logger.Debug("Menu Item click detected, Swaping Show/Hide.");
            this.customToggle();
        }

        private void AutoDialIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            m_logger.Debug("Icon DoubleClick detected, Swaping Show/Hide.");
            this.customToggle();
        }

        #endregion

        #region Hiding


        private void customToggle()
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                customHide();
            }
            else
            {
                customShow();
            }
        }

        private void customHide()
        {
            Hide();//hides the program on the taskbar
            this.WindowState = FormWindowState.Minimized;//undoes the minimized state of the form
            AutoDialIcon.Visible = true;//shows our tray icon
        }

        private void customShow()
        {
            Show();//shows the program on taskbar
            this.WindowState = FormWindowState.Normal;//undoes the minimized state of the form
            //notifyIcon1.Visible = false;//hides tray icon again

        }

        #endregion

        #region Number Output

        public void callNumber(string number)
        {
            m_logger.Debug("Start: callNumber(), input number is:" + number);

            //Check number Override

            string numberOverride = System.Configuration.ConfigurationManager.AppSettings["numberOverride"];

            if (numberOverride == null)
            {
                m_logger.Error("\"numberOverride\" not found in config, aborting call");
                this.errorPopup("numberOverride not found" ,"\"numberOverride\" not found in config, aborting call");
                return;
            }

            if (numberOverride == "")
            {
                m_logger.Info("numberOverride is blank, continuing call");
            }
            else
            {
                m_logger.Info("numberOverride detected in config file, changing from detected number:" + number + " to calling: " + numberOverride);
                number = numberOverride;
            }

            // Location of the program (talk) used to dial the found number.
            string programFileLocation = System.Configuration.ConfigurationManager.AppSettings["programFileLocation"];
            if (programFileLocation != null)
            {
                if (System.IO.File.Exists(programFileLocation))
                {
                    if (m_logAndErr.isFirstError())
                    {
                        AutoDialIcon.ShowBalloonTip(3000, "Calling number", "Calling: " + number, ToolTipIcon.Info);
                    }
                    m_logger.Info("Calling: " + number);

                    //System.Diagnostics.Process newProcess = new Process();
                    //string fileLocation = @"G:\Programs\Notepad++\notepad++.exe";
                    string param = @"-dial """ + number + @" "" ";

                    System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(programFileLocation, param);

                    // The following commands are needed to redirect the standard output.
                    // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    //procStartInfo.RedirectStandardOutput = false;
                    //procStartInfo.UseShellExecute = true;
                    // Do not create the black window.
                    //procStartInfo.CreateNoWindow = false;
                    // Now we create a process, assign its ProcessStartInfo and start it
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.StartInfo = procStartInfo;
                    proc.Start();

                    //proc.WaitForExit(1000);
                    bringToFront();
                    //proc.WaitForExit();
                }
                else
                {
                    m_logger.Error("File location found in config but not on system, Please check path is correct for current machine." + programFileLocation);
                    this.errorPopup("Talk Program not found", "Talk File location found in config but not on system, Please check path is correct for current machine." + programFileLocation);
                }
            }
            else
            {
                m_logger.Error("Unable to locate \"programFileLocation\" in Config file.");
                this.errorPopup("Talk Program not found in config", "Unable to locate \"programFileLocation\" in Config file.");
            }
        }


        public void bringToFront()
        {

            string processName = System.Configuration.ConfigurationManager.AppSettings["talkProcessName"];

            if (processName == null)
            {
                m_logger.Error("talkProcessName is not found in config, unable to bring up window.");
                this.errorPopup("talkProcessName is not found", "talkProcessName not foind in config file, please check file");
                return;
            }
            
            Process[] processArray = Process.GetProcessesByName(processName);

            if (processArray.Count() == 0)
            {
                m_logger.Error("Talk process not found: " + processName + Environment.NewLine + "Please check that the has opened correctly and is running, if so then check that the configured processName is correct");

                this.errorPopup("Error", "Talk process not found, unable to automatically bring up talk window: " + processName + Environment.NewLine + "Please check that the program is running, if so then check that the configured processName is correct");
            }
            else
            {
                m_logger.Debug("Found " + processArray.Count() + " matching processes");
            }


            Process process = processArray[0]; 
            for (int i = 0; i < processArray.Count(); ++i)
            {
                if (processArray[i].MainWindowTitle != "")
                {
                    process = processArray[i];
                }
            }
            
            // process = processArray[0];

            User32.SetForegroundWindow(process.MainWindowHandle);
        }

        #endregion

        #region GenerateFileNames

        public string generateNameInitial(string dateStamp)
        {
            return @"C:\AutoDial\Images\image_" + dateStamp + ".png";
        }

        public string generateNameGrey(string dateStamp)
        {
            return @"C:\AutoDial\Images\image_" + dateStamp + "_grey.png";
        }

        #endregion

        /// <summary>
        /// Create an error popup if there has not been any before now
        /// </summary>
        /// <param name="title">The title of the Error</param>
        /// <param name="content">The content of the Error</param>
        public void errorPopup(string title, string content)
        {
            if (!m_logAndErr.isFirstError())
            {
                m_logAndErr.setFirstErrorSignalled(true);

                m_logger.Error("Showing Error popup: " + title + "_" + content);
                AutoDialIcon.ShowBalloonTip(5000, title, content, ToolTipIcon.Error);
            }
            else
            {
                m_logger.Error("Supressed Error popup: " + title + "_" + content);
            }
        }

        public void cleanupFolder()
        {
            
            string cleanupFolderConfig = System.Configuration.ConfigurationManager.AppSettings["cleanupFolder"];
            if (cleanupFolderConfig != null)
            {
                if (cleanupFolderConfig == "true")
                {
                     //Remove files
                    string[] files = System.IO.Directory.GetFiles(@"C:\AutoDial");

                    foreach (string file in files)
                    {
                        System.IO.File.Delete(file);
                    }

                    files = System.IO.Directory.GetFiles(@"C:\AutoDial\log");

                    foreach (string file in files)
                    {
                        System.IO.File.Delete(file);
                    }
                }


            }


        }

    }

}
/*
 
 * 
        /// <summary>
        /// Capture an image of the enture screen and save it to a file.
        /// </summary>
        public void CaptureScreen()
        {
            //Create a new bitmap.
            Bitmap bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                           Screen.PrimaryScreen.Bounds.Height,
                                           PixelFormat.Format32bppArgb);

            // Create a graphics object from the bitmap.
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Take the screenshot from the upper left corner to the right bottom corner.
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                        Screen.PrimaryScreen.Bounds.Y,
                                        0,
                                        0,
                                        Screen.PrimaryScreen.Bounds.Size,
                                        CopyPixelOperation.SourceCopy);

            // Save the screenshot to the specified path that the user has chosen.
            bmpScreenshot.Save("Screenshot.png", System.Drawing.Imaging.ImageFormat.Png);
        }

 
 */