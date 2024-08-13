using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
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

namespace DynamicIslandOverlay
{
    public partial class MainWindow : Window
    {
        
        private TaskbarIcon _trayIcon;
        private DispatcherTimer _timer;

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

        public class CpuInfo
        {
            public static float GetCpuUsage()
            {
                float cpuUsage = 0;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfFormattedData_PerfOS_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        cpuUsage += float.Parse(obj["PercentProcessorTime"].ToString());
                    }
                }
                return cpuUsage;
            }
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
            HideIsland();

            // Initialize the DispatcherTimer
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Update every 10 seconds
            };
            _timer.Tick += UpdateStats; // Update stats every tick
            _timer.Start();

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
                Icon = new System.Drawing.Icon("Assets/iconong.ico"), // Update with the correct relative path
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
                Header = "Game Mode",
                IsCheckable = true,
                IsChecked = IsGameMode
            };

            toggleGameModeMenuItem.Click += (s, e) =>
            {
                IsGameMode = !IsGameMode;
                toggleGameModeMenuItem.IsChecked = IsGameMode;
                // Optionally update UI or other states based on the new game mode value
                UpdateGameMode();
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
            contextMenu.Items.Add(quitMenuItem);

            return contextMenu;
        }

        private void UpdateGameMode()
        {
            IsGameMode = !IsGameMode;
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

                float cpuUsage = CpuInfo.GetCpuUsage();
                this.WindowslandText.Text = "CPU: " + cpuUsage.ToString() + "%";
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

        private void HoverTrigger_MouseEnter(Object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsGameMode && TriggerButtonOnScreen == false)
            {
                pathanimation.From = 0;
                pathanimation.To = 16;
                BulgePath.BeginAnimation(Path.HeightProperty, pathanimation);
                Arrow.BeginAnimation(System.Windows.Controls.Image.HeightProperty, pathanimation);
                pulldownbutton.BeginAnimation(System.Windows.Controls.Button.HeightProperty, pathanimation);
                TriggerButtonOnScreen = true;
            }
        }

        private void HoverTrigger_MouseLeave(Object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsGameMode && TriggerButtonOnScreen == true)
            {
                pathanimation.From = 16;
                pathanimation.To = 0;
                BulgePath.BeginAnimation(Path.HeightProperty, pathanimation);
                Arrow.BeginAnimation(System.Windows.Controls.Image.HeightProperty, pathanimation);
                pulldownbutton.BeginAnimation(System.Windows.Controls.Button.HeightProperty, pathanimation);
                TriggerButtonOnScreen = false;
            }
        }

        private void safeAreaEnter(Object sender, System.Windows.Input.MouseEventArgs e)
        {
            safearea = true;
        }

        private void safeAreaLeave(Object sender, System.Windows.Input.MouseEventArgs e)
        {
            safearea = false;
        }

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
                DoubleAnimation widthAnimation = CreateAnimation(270, 0, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(WidthProperty, widthAnimation);

                DoubleAnimation leftAnimation = CreateAnimation(30, 152, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(Canvas.LeftProperty, leftAnimation);

                DoubleAnimation OpacityAnimation = CreateAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(UIElement.OpacityProperty, OpacityAnimation);

                IsIslandHidden = true;

                pathanimation.From = 16;
                pathanimation.To = 0;
                BulgePath.BeginAnimation(Path.HeightProperty, pathanimation);
                Arrow.BeginAnimation(System.Windows.Controls.Image.HeightProperty, pathanimation);
                pulldownbutton.BeginAnimation(System.Windows.Controls.Button.HeightProperty, pathanimation);
                TriggerButtonOnScreen = false;
            }
            else
            {
                DoubleAnimation widthAnimation = CreateAnimation(0, 270, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(WidthProperty, widthAnimation);

                DoubleAnimation leftAnimation = CreateAnimation(152, 30, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(Canvas.LeftProperty, leftAnimation);

                DoubleAnimation OpacityAnimation = CreateAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                Island.BeginAnimation(UIElement.OpacityProperty, OpacityAnimation);

                IsIslandHidden = false;

                pathanimation.From = 16;
                pathanimation.To = 0;
                BulgePath.BeginAnimation(Path.HeightProperty, pathanimation);
                Arrow.BeginAnimation(System.Windows.Controls.Image.HeightProperty, pathanimation);
                pulldownbutton.BeginAnimation(System.Windows.Controls.Button.HeightProperty, pathanimation);
                TriggerButtonOnScreen = false;

                InitializeIslandElements();
            }
        }

        private void PullDownButton_Click(object sender, RoutedEventArgs e)
        {
            HideIsland();
        }
    }
}