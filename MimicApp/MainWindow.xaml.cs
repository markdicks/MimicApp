using Gma.System.MouseKeyHook;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using WindowsInput;
using WindowsInput.Native;

namespace MimicApp
{
    public partial class MainWindow : Window
    {
        private IKeyboardMouseEvents _hookManager;
        private List<InputAction> _actions;
        private DateTime _lastActionTime;
        private string _defaultFilePath = "recordedActions.json";
        private string _filePath;
        private InputSimulator _inputSimulator;
        private YourNamespace.TransparentOverlayWindow _overlay; // Overlay instance
        private double _playbackSpeed = 1.0;
        private int _playbackRepeats = 1; // Number of times to repeat playback
        private bool _emergencyStopRequested = false;

        public MainWindow()
        {
            InitializeComponent();
            _actions = new List<InputAction>();
            _inputSimulator = new InputSimulator();
            _overlay = new YourNamespace.TransparentOverlayWindow(this);

            ResetApplication(); // Ensure app starts in default state
        }

        // Start recording inputs
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _actions.Clear();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            _lastActionTime = DateTime.Now;

            // Show the transparent overlay to block input
            _overlay.Show();
            this.Activate();

            _hookManager = Hook.GlobalEvents();
            _hookManager.KeyDown += HookManager_KeyDown;
            _hookManager.MouseClick += HookManager_MouseClick;
            _hookManager.MouseMove += HookManager_MouseMove;
            _hookManager.MouseDown += HookManager_MouseDown;
            _hookManager.MouseUp += HookManager_MouseUp;
        }

        // Remove the last action
        private void RemoveLastAction()
        {
            if (_actions.Count > 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    _actions.RemoveAt(_actions.Count - 1);
                }
            }
        }

        // Stop recording and save to a file
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;
            PlayButton.IsEnabled = true;

            _hookManager.KeyDown -= HookManager_KeyDown;
            _hookManager.MouseClick -= HookManager_MouseClick;
            _hookManager.MouseMove -= HookManager_MouseMove;
            _hookManager.MouseDown -= HookManager_MouseDown;
            _hookManager.MouseUp -= HookManager_MouseUp;

            _hookManager.Dispose();

            // Close the overlay as recording has stopped
            _overlay.Hide();

            RemoveLastAction();

            // Determine file path
            var fileName = FileNameTextBox.Text;
            _filePath = string.IsNullOrEmpty(fileName) ? _defaultFilePath : $"{fileName}.json";

            File.WriteAllText(_filePath, JsonSerializer.Serialize(_actions));
            System.Windows.Forms.MessageBox.Show($"Recording saved to {_filePath}");

            // Automatically open the file saved
            // System.Diagnostics.Process.Start("notepad.exe", _filePath);
        }

        // Play back the recorded inputs
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_filePath))
            {
                System.Windows.Forms.MessageBox.Show("No recording found.");
                return;
            }

            // Load playback repeats from the advanced settings
            if (!int.TryParse(PlaybackRepeatsTextBox.Text, out _playbackRepeats) || _playbackRepeats < 1)
            {
                _playbackRepeats = 1; // Default to 1 if invalid
            }
            _emergencyStopRequested = false; // Reset the stop flag

            _actions = JsonSerializer.Deserialize<List<InputAction>>(File.ReadAllText(_filePath));

            Task emergencyStopTask = Task.Run(() => ListenForEmergencyStop()); // Start the listener for stop

            // Repeat playback according to the user input
            for (int i = 0; i < _playbackRepeats; i++)
            {
                foreach (var action in _actions)
                {
                    if (_emergencyStopRequested)
                    {
                        System.Windows.Forms.MessageBox.Show("Playback stopped by emergency stop (Ctrl+C).");
                        return; // Exit playback
                    }
                    System.Threading.Thread.Sleep((int)(action.Delay / _playbackSpeed)); // Apply playback speed

                    if (action.Type == InputActionType.Key)
                    {
                        _inputSimulator.Keyboard.KeyPress((VirtualKeyCode)action.KeyCode);
                    }
                    else if (action.Type == InputActionType.MouseClick)
                    {
                        _inputSimulator.Mouse.MoveMouseTo(action.MouseX, action.MouseY);
                        //_inputSimulator.Mouse.LeftButtonClick();
                    }
                    else if (action.Type == InputActionType.MouseDrag)
                    {
                        // Move to the starting position and hold down the mouse button
                        _inputSimulator.Mouse.MoveMouseTo(action.StartMouseX, action.StartMouseY);
                        _inputSimulator.Mouse.LeftButtonDown(); // Hold down the mouse button

                        // Move through each recorded drag position
                        foreach (var position in action.DragPositions)
                        {
                            _inputSimulator.Mouse.MoveMouseTo(position.MouseX, position.MouseY);
                            System.Threading.Thread.Sleep((int)(position.Delay / _playbackSpeed)); // Apply playback speed for drag timing
                        }

                        // Release the mouse button at the end of the drag
                        _inputSimulator.Mouse.LeftButtonUp();
                    }
                }
                if (_emergencyStopRequested)
                {
                    break;
                }
            }
            if (!_emergencyStopRequested)
            {
                System.Windows.Forms.MessageBox.Show("Playback complete.");
            }
        }

        private void ListenForEmergencyStop()
        {
            while (!_emergencyStopRequested)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.C))
                    {
                        _emergencyStopRequested = true;
                    }
                });

                System.Threading.Thread.Sleep(50);
            }
        }

        // File selection dialog
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                InitialDirectory = Directory.GetCurrentDirectory()
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _filePath = openFileDialog.FileName;
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = false;
                PlayButton.IsEnabled = true;
                System.Windows.Forms.MessageBox.Show($"File loaded: {_filePath}");
            }
        }

        // Reset application to default state
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetApplication();
        }

        // Reset method to restore app to its initial state
        private void ResetApplication()
        {
            _actions.Clear();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PlayButton.IsEnabled = false;
            _filePath = _defaultFilePath;
            PlaybackRepeatsTextBox.Text = "1";
            FileNameTextBox.Text = string.Empty;
            System.Windows.Forms.MessageBox.Show("Application has been reset to default state.");
        }

        // Capture keyboard key press
        private void HookManager_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            var delay = (DateTime.Now - _lastActionTime).Milliseconds;
            _lastActionTime = DateTime.Now;

            _actions.Add(new InputAction
            {
                Type = InputActionType.Key,
                KeyCode = (int)e.KeyCode,
                Delay = delay
            });
        }

        // Capture mouse click
        private void HookManager_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var delay = (DateTime.Now - _lastActionTime).Milliseconds;
            _lastActionTime = DateTime.Now;

            _actions.Add(new InputAction
            {
                Type = InputActionType.MouseClick,
                MouseX = (int)(e.X / (double)Screen.PrimaryScreen.Bounds.Width * 65535),
                MouseY = (int)(e.Y / (double)Screen.PrimaryScreen.Bounds.Height * 65535),
                Delay = delay
            });
        }

        // Capture mouse movement
        private void HookManager_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_actions.Count > 0 && _actions[^1].IsDragging)
            {
                var delay = (DateTime.Now - _lastActionTime).Milliseconds;
                _lastActionTime = DateTime.Now;

                // Record the current position during drag
                _actions[^1].DragPositions.Add(new DragPosition
                {
                    MouseX = (int)(e.X / (double)Screen.PrimaryScreen.Bounds.Width * 65535),
                    MouseY = (int)(e.Y / (double)Screen.PrimaryScreen.Bounds.Height * 65535),
                    Delay = delay
                });
            }
            else if (_actions.Count == 0 || !_actions[^1].IsDragging)
            {
                var delay = (DateTime.Now - _lastActionTime).Milliseconds;
                _lastActionTime = DateTime.Now;

                _actions.Add(new InputAction
                {
                    Type = InputActionType.MouseMove,
                    MouseX = (int)(e.X / (double)Screen.PrimaryScreen.Bounds.Width * 65535),
                    MouseY = (int)(e.Y / (double)Screen.PrimaryScreen.Bounds.Height * 65535),
                    Delay = delay
                });
            }
        }

        // Capture mouse down (for drag start)
        private void HookManager_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _lastActionTime = DateTime.Now;
                var delay = 0;

                var dragAction = new InputAction
                {
                    Type = InputActionType.MouseDrag,
                    StartMouseX = (int)(e.X / (double)Screen.PrimaryScreen.Bounds.Width * 65535),
                    StartMouseY = (int)(e.Y / (double)Screen.PrimaryScreen.Bounds.Height * 65535),
                    Delay = delay,
                    DragPositions = new List<DragPosition>()
                };
                _actions.Add(dragAction);
            }
        }

        // Capture mouse up (for drag end)
        private void HookManager_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _actions.Count > 0 && _actions[^1].Type == InputActionType.MouseDrag)
            {
                var delay = (DateTime.Now - _lastActionTime).Milliseconds;
                _lastActionTime = DateTime.Now;

                _actions[^1].EndMouseX = (int)(e.X / (double)Screen.PrimaryScreen.Bounds.Width * 65535);
                _actions[^1].EndMouseY = (int)(e.Y / (double)Screen.PrimaryScreen.Bounds.Height * 65535);
                _actions[^1].Delay = delay;
            }
        }
        private void PlaybackSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _playbackSpeed = e.NewValue; // Update the playback speed multiplier
        }

    }

    // Define the input action types and structure
    public class InputAction
    {
        public InputActionType Type { get; set; }
        public int KeyCode { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public int StartMouseX { get; set; }
        public int StartMouseY { get; set; }
        public int EndMouseX { get; set; }
        public int EndMouseY { get; set; }
        public int Delay { get; set; }
        public List<DragPosition> DragPositions { get; set; } = new List<DragPosition>();
        public bool IsDragging => Type == InputActionType.MouseDrag;
    }

    public class DragPosition
    {
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public int Delay { get; set; }
    }

    public enum InputActionType
    {
        Key,
        MouseClick,
        MouseMove,
        MouseDrag
    }
}
