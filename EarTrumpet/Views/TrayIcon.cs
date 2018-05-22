﻿using EarTrumpet.DataModel;
using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Misc;
using EarTrumpet.Services;
using EarTrumpet.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using resx = EarTrumpet.Properties.Resources;

namespace EarTrumpet.Views
{
    class TrayIcon
    {
        private readonly System.Windows.Forms.NotifyIcon _trayIcon;
        private readonly TrayViewModel _trayViewModel;
        private readonly MainViewModel _mainViewModel;
        private readonly IVirtualDefaultAudioDevice _defaultDevice;

        public TrayIcon(IAudioDeviceManager deviceManager, MainViewModel mainViewModel, TrayViewModel trayViewModel)
        {
            _mainViewModel = mainViewModel;
            _defaultDevice = deviceManager.VirtualDefaultDevice;
            _defaultDevice.PropertyChanged += (_, __) => UpdateToolTip();

            _trayViewModel = trayViewModel;
            _trayViewModel.PropertyChanged += TrayViewModel_PropertyChanged;

            _trayIcon = new System.Windows.Forms.NotifyIcon();
            _trayIcon.MouseClick += TrayIcon_MouseClick;
            _trayIcon.Icon = _trayViewModel.TrayIcon;
            UpdateToolTip();

            _trayIcon.Visible = true;
        }

        private ContextMenu BuildContextMenu()
        {
            var cm = new ContextMenu();

            // TODO: add a style.

            cm.FlowDirection = UserSystemPreferencesService.IsRTL ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            cm.Opened += (_, __) =>
            {
                User32.SetForegroundWindow(((HwndSource)HwndSource.FromVisual(cm)).Handle);
                cm.Focus();
            };

            var AddItem = new Action<string, ICommand>((displayName, action) =>
            {
                cm.Items.Add(new MenuItem
                {
                    Header = displayName,
                    Command = action
                });
            });

            // Add devices
            var audioDevices = _mainViewModel.AllDevices.OrderBy(x => x.DisplayName);
            if (!audioDevices.Any())
            {
                cm.Items.Add(new MenuItem
                {
                    Header = resx.ContextMenuNoDevices,
                    IsEnabled = false
                });
            }
            else
            {
                foreach (var device in audioDevices)
                {
                    cm.Items.Add(new MenuItem
                    {
                        Header = device.DisplayName,
                        IsChecked = device.Id == _defaultDevice.Id,
                        Command = new RelayCommand(() => _trayViewModel.ChangeDeviceCommand.Execute(device)),
                    });
                }
            }

            // Static items
            cm.Items.Add(new Separator());
            AddItem(resx.FullWindowTitleText, _trayViewModel.OpenEarTrumpetVolumeMixerCommand);
            AddItem(resx.LegacyVolumeMixerText, _trayViewModel.OpenLegacyVolumeMixerCommand);
            cm.Items.Add(new Separator());
            AddItem(resx.PlaybackDevicesText, _trayViewModel.OpenPlaybackDevicesCommand);
            AddItem(resx.RecordingDevicesText, _trayViewModel.OpenRecordingDevicesCommand);
            AddItem(resx.SoundsControlPanelText, _trayViewModel.OpenSoundsControlPanelCommand);
            cm.Items.Add(new Separator());
            AddItem(resx.SettingsWindowText, _trayViewModel.OpenSettingsCommand);
            AddItem(resx.ContextMenuSendFeedback, _trayViewModel.StartAppServiceAndFeedbackHubCommand);
            AddItem(resx.ContextMenuExitTitle, new RelayCommand(Exit_Click));

            return cm;
        }

        private void UpdateToolTip()
        {
            if (_defaultDevice.IsDevicePresent)
            {
                var otherText = "EarTrumpet: 100% - ";
                var dev = _defaultDevice.DisplayName;
                // API Limitation: "less than 64 chars" for the tooltip.
                dev = dev.Substring(0, Math.Min(63 - otherText.Length, dev.Length));
                _trayIcon.Text = $"EarTrumpet: {_defaultDevice.Volume.ToVolumeInt()}% - {dev}";
            }
            else
            {
                _trayIcon.Text = resx.NoDeviceTrayText;
            }
        }

        private void TrayViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_trayViewModel.TrayIcon))
            {
                _trayIcon.Icon = _trayViewModel.TrayIcon;
            }
        }

        void TrayIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _trayViewModel.OpenFlyoutCommand.Execute();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                var cm = BuildContextMenu();
                cm.Placement = PlacementMode.Mouse;
                cm.IsOpen = true;
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Middle)
            {
                _defaultDevice.IsMuted = !_defaultDevice.IsMuted;
            }
        }

        public void Exit_Click()
        {
            try
            {
                foreach(var proc in Process.GetProcessesByName("EarTrumpet.UWP"))
                {
                    proc.Kill();
                }
            }
            catch
            {
                // We're shutting down, ignore all
            }
            FeedbackService.CloseAppService();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}
