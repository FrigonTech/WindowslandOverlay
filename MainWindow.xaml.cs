using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using OpenHardwareMonitor.Hardware;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Data;
using System.Configuration;
using System.Runtime.CompilerServices;
using IWshRuntimeLibrary;
using System.IO;

namespace DynamicIslandOverlay
{
    public partial class MainWindow : Window
    {
        
        private TaskbarIcon _trayIcon;
        private DispatcherTimer _timer;

        private const string AppName = "DynamicIslandOverlay";

        // IsLaptop
        public bool IsLaptop()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                var batteryCount = searcher.Get().Count;
                return batteryCount > 0;
            }
            catch
            {
                return false;
            }
        }

        private double angle = 0; // Initial angle

        private void OnRendering(object sender, EventArgs e)
        {
            // Increase angle based on time for smoother animation
            angle += 2; // Adjust this value for speed (degrees per frame)
            if (angle >= 360)
                angle = 0; // Reset angle to avoid overflow

            // Apply the new angle
            RotatingTransform.Angle = angle;
            RotatingTransform1.Angle = (angle)*-1;
        }

        public static float GetAverageCpuUsage()
        {
            float totalCpuUsage = 0;
            int processorCount = 0;

            try
            {
                // Query the WMI for processor load percentages
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    // Increment the processor count and add to the total CPU usage
                    processorCount++;
                    totalCpuUsage += float.Parse(obj["LoadPercentage"].ToString());
                }

                // Calculate the mean CPU usage
                if (processorCount > 0)
                {
                    return totalCpuUsage / processorCount;
                }
                else
                {
                    // Handle the case where no processors were found
                    return 0;
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions as needed
                Console.WriteLine("Error: " + ex.Message);
                return 0;
            }
        }

        private void StartMonitoring()
        {
            if (_timer == null)
            {
                // Initialize the DispatcherTimer
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1) // Update every second
                };
                _timer.Tick += UpdateStats; // Update stats every tick
            }

            _timer.Start();
        }

        private void StopMonitoring()
        {
            _timer?.Stop(); // Stop the timer if it's running
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            this.StateChanged += OnWindowStateChanged;
            PositionWindow();
            DataContext = this;
            this.Island.BorderBrush = new SolidColorBrush(IslandColor);
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            BatteryChargingAnimation();
            CompositionTarget.Rendering += OnRendering;

            // Start by hiding the island, which will also handle the initial state of the monitoring
            HideIsland();

            // Initial call to set the time, date, and stats immediately
            UpdateStats(null, null);
        }


        // CurrentIslandColor
        System.Windows.Media.Color IslandColor = Colors.Cyan;

        // IsBatteryCharging
        public bool IsBatteryCharging()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var batteryStatus = obj["BatteryStatus"];
                    return batteryStatus != null && (Convert.ToUInt16(batteryStatus) == 2); // 2 = Charging
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Enabled?
        bool IsGameMode = false;
        bool safearea = false;
        bool IsIslandHidden = false;

        // CommonAnim
        DoubleAnimation pathanimation = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromSeconds(0.15)),
        };

        DoubleAnimation SingleFloatAnimation = new DoubleAnimation
        {
        };

        ThicknessAnimation borderthicknessanim = new ThicknessAnimation
        {
            Duration = new Duration(TimeSpan.FromSeconds(0.15)),
        };

        ColorAnimation colorAnimation = new ColorAnimation
        {
            Duration = new Duration(TimeSpan.FromSeconds(0.15))
        };

        private void InitializeTrayIcon()
        {

            _trayIcon = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon("Assets/WLicon.ico"), // Relative path to the output directory
                ToolTipText = "Dynamic Island Overlay",
                ContextMenu = CreateContextMenu()
            };

            if (_trayIcon == null)
            {
                System.Windows.MessageBox.Show("TrayIcon is not initialized.");
            }
        }

        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            var toggleGameModeMenuItem = new MenuItem
            {
                Header = "On/Off",
                IsCheckable = true,
                IsChecked = IsIslandHidden
            };

            toggleGameModeMenuItem.Click += (s, e) =>
            {
                toggleGameModeMenuItem.IsChecked = IsIslandHidden;
                // Optionally update UI or other states based on the new game mode value
                UpdateGameMode();
            };

            var toggleStartupMenuItem = new MenuItem
            {
                Header = "Enable/Disable Startup",
                IsCheckable = true,
                IsChecked = IsAppSetToRunAtStartup()
            };
            toggleStartupMenuItem.Click += (s, e) =>
            {
                if (toggleStartupMenuItem.IsChecked)
                {
                    SetAppToRunAtStartup(true);
                }
                else
                {
                    SetAppToRunAtStartup(false);
                }
            };

            var quitMenuItem = new MenuItem
            {
                Header = "Quit"
            };

            quitMenuItem.Click += (s, e) =>
            {
                System.Windows.Application.Current.Shutdown(); // Close the application
            };

            contextMenu.Items.Add(toggleGameModeMenuItem);
            contextMenu.Items.Add(toggleStartupMenuItem);
            contextMenu.Items.Add(quitMenuItem);
            

            return contextMenu;
        }

        private void SetAppToRunAtStartup(bool enable)
        {
            if (enable)
            {
                AddToStartup();
            }
            else
            {
                RemoveFromStartup();
            }
        }

        private void AddToStartup()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; // Correct path to the executable
            string shortcutPath = System.IO.Path.Combine(startupFolder, $"{AppName}.lnk");
            CreateShortcut(shortcutPath, exePath);
        }

        private void RemoveFromStartup()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = System.IO.Path.Combine(startupFolder, $"{AppName}.lnk");
            if (System.IO.File.Exists(shortcutPath))
            {
                System.IO.File.Delete(shortcutPath);
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath)
        {
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath);
            shortcut.IconLocation = targetPath; // Optional: Set the icon
            shortcut.Save();
        }

        private bool IsAppSetToRunAtStartup()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = System.IO.Path.Combine(startupFolder, $"{AppName}.lnk");
            return System.IO.File.Exists(shortcutPath);
        }

        private void UpdateGameMode()
        {
            HideIsland();
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                _trayIcon.Visibility = Visibility.Visible;
            }
        }

        private void UpdateStats(object sender, EventArgs e)
        {
            try
            {
                // Update time
                this.WindowslandTime.Text = DateTime.Now.ToString("HH:mm tt");

                // Update date
                this.WindowslandDate.Text = DateTime.Now.ToString("ddd, dd MMMM")
                    .Replace("January", "Jan").Replace("February", "Feb").Replace("March", "Mar")
                    .Replace("April", "Apr").Replace("May", "May").Replace("June", "Jun")
                    .Replace("July", "Jul").Replace("August", "Aug").Replace("September", "Sep")
                    .Replace("October", "Oct").Replace("November", "Nov").Replace("December", "Dec");

                float cpuUsage = GetAverageCpuUsage();
                this.WindowslandText.Text = "CPU: " + Math.Floor(cpuUsage).ToString() + "%";
            }
            catch (Exception ex)
            {
                // Handle or log the exception as needed
                this.WindowslandText.Text = $"Error: {ex.Message}";
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.StatusChange)
            {
                BatteryChargingAnimation();
            }
        }

        private void InitializeIslandElements()
        {
            SingleFloatAnimation.From = 0;
            SingleFloatAnimation.To = 1;
            SingleFloatAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4));
            this.WindowslandTime.BeginAnimation(OpacityProperty, SingleFloatAnimation);
            this.WindowslandDate.BeginAnimation(OpacityProperty, SingleFloatAnimation);
            this.WindowslandText.BeginAnimation(OpacityProperty, SingleFloatAnimation);
        }

        private void PositionWindow()
        {
            var screenwidth = SystemParameters.WorkArea.Width;
            var screenheight = SystemParameters.WorkArea.Height;

            var targetwidth = screenwidth / 6;
            var targetheight = screenheight / 14;

            this.Width = targetwidth;
            this.Height = targetheight;

            var left = (screenwidth - Width) / 2;

            this.Left = left;
            this.Top = 0;

            this.Topmost = Topmost;
        }

        bool TriggerButtonOnScreen = false;

        private void BatteryChargingAnimation()
        {
            bool isCharging = IsBatteryCharging(); // Store the result of IsBatteryCharging()

            // Color animation: Change to green if charging, revert to original color if not
            colorAnimation.From = isCharging ? IslandColor : Colors.LightGreen;
            colorAnimation.To = isCharging ? Colors.LightGreen : IslandColor;

            // Thickness animation: Animate border thickness based on charging status
            borderthicknessanim.From = isCharging ? new Thickness(1) : new Thickness(3);
            borderthicknessanim.To = isCharging ? new Thickness(3) : new Thickness(1);

            // Start animations
            Island.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            Island.BeginAnimation(Border.BorderThicknessProperty, borderthicknessanim);
        }

        private DoubleAnimation CreateAnimation(double from, double to, Duration duration)
        {
            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration
            };
        }

        public void HideIsland()
        {
            if (!IsIslandHidden)
            {
                // Play the hide animation
                DoubleAnimation widthAnimation = CreateAnimation(270, 0, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(WidthProperty, widthAnimation);

                DoubleAnimation leftAnimation = CreateAnimation(30, 152, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(Canvas.LeftProperty, leftAnimation);

                DoubleAnimation opacityAnimation = CreateAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);

                // Stop monitoring when hiding the island
                StopMonitoring();

                IsIslandHidden = true;
            }
            else
            {
                // Play the show animation
                DoubleAnimation widthAnimation = CreateAnimation(0, 270, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(WidthProperty, widthAnimation);

                DoubleAnimation leftAnimation = CreateAnimation(152, 30, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(Canvas.LeftProperty, leftAnimation);

                DoubleAnimation opacityAnimation = CreateAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);

                // Initialize island elements only when showing the island
                InitializeIslandElements();

                // Start monitoring when showing the island
                StartMonitoring();

                IsIslandHidden = false;
            }
        }
    }
}