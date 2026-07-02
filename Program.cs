using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OpenKVM
{
    // ========================================================================
    // 1. GLOBAL SYSTEM PERSISTENCE MODEL
    // ========================================================================
    public class KVMConfig
    {
        public bool IsMaster = true;
        public string TargetIP = "192.168.1.15";
        public int EdgeDirection = 0;      // 0=Right, 1=Left, 2=Top, 3=Bottom
        public int EdgeThickness = 3;      
        public int NetworkCadenceHz = 120; 
        public bool InvertMouse = false;   
        public bool MuteLogs = false;      
        public string SecurityKey = "KVM_Secure_Pass1"; 

        private readonly string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openkvm.cfg");

        public void Save()
        {
            try {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[OPENKVM_SHIELD_CONFIG]");
                sb.AppendLine($"IsMaster={IsMaster}");
                sb.AppendLine($"TargetIP={TargetIP}");
                sb.AppendLine($"EdgeDirection={EdgeDirection}");
                sb.AppendLine($"EdgeThickness={EdgeThickness}");
                sb.AppendLine($"NetworkCadenceHz={NetworkCadenceHz}");
                sb.AppendLine($"InvertMouse={InvertMouse}");
                sb.AppendLine($"MuteLogs={MuteLogs}");
                sb.AppendLine($"SecurityKey={SecurityKey}");
                File.WriteAllText(filePath, sb.ToString());
            } catch { }
        }

        public void Load()
        {
            if (!File.Exists(filePath)) return;
            try {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines) {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("[")) continue;
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    string key = parts[0].Trim(); string val = parts[1].Trim();

                    if (key == "IsMaster") bool.TryParse(val, out IsMaster);
                    else if (key == "TargetIP") TargetIP = val;
                    else if (key == "EdgeDirection") int.TryParse(val, out EdgeDirection);
                    else if (key == "EdgeThickness") int.TryParse(val, out EdgeThickness);
                    else if (key == "NetworkCadenceHz") int.TryParse(val, out NetworkCadenceHz);
                    else if (key == "InvertMouse") bool.TryParse(val, out InvertMouse);
                    else if (key == "MuteLogs") bool.TryParse(val, out MuteLogs);
                    else if (key == "SecurityKey") SecurityKey = val;
                }
            } catch { }
        }
    }

    // ========================================================================
    // 2. MAIN PRODUCTION APPLICATION WINDOW
    // ========================================================================
    public class KVMForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookHandler lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private delegate IntPtr HookHandler(int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        private const int WH_KEYBOARD_LL = 13; private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201; private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204; private const int WM_RBUTTONUP = 0x0205;
        private const int WM_KEYDOWN = 0x0100;    private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104; private const int WM_SYSKEYUP = 0x0105;

        private KVMConfig config = new KVMConfig();
        private bool isRunning = false;
        private static bool remoteMode = false;
        private static UdpClient? udpSender;
        private static IPEndPoint? sendEndPoint;
        
        private IntPtr mouseHookID = IntPtr.Zero;
        private IntPtr keyboardHookID = IntPtr.Zero;
        private HookHandler? mouseHookProcedure;
        private HookHandler? keyboardHookProcedure;
        private Thread? listenerThread;
        private Thread? fileServerThread;
        private Thread? discoveryThread;
        private Thread? slaveHeartbeatThread;
        private System.Windows.Forms.Timer edgeWatchTimer;
        private TcpListener? fileListener;
        private UdpClient? discoveryBeaconClient;

        private static DateTime lastSlaveHeartbeatTimestamp = DateTime.Now;

        private ComboBox cmbMode; private ComboBox cmbEdge;
        private TextBox txtIP; private TextBox txtCryptoKey; private ListBox lstLog;
        private Button btnEngage;
        private TrackBar trkThickness; private TrackBar trkHz;
        private Label lblThickness; private Label lblHz;
        private CheckBox chkInvert; private CheckBox chkMute;
        private NotifyIcon trayIcon;
        private IContainer components;

        public KVMForm()
        {
            config.Load();
            components = new Container();

            Text = "OpenKVM Suite v5.0 (Admin Privileged)"; Width = 475; Height = 750;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            this.AllowDrop = true; this.DragEnter += KVMForm_DragEnter; this.DragDrop += KVMForm_DragDrop;

            trayIcon = new NotifyIcon(components) { Icon = SystemIcons.Shield, Text = "OpenKVM Shield Core Active", Visible = true };
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Restore Interface Deck", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add("Terminate OpenKVM", null, (s, e) => { CleanShutdown(); Application.Exit(); });
            trayIcon.ContextMenuStrip = trayMenu; trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };

            // --- DECK 1: NETWORK REGISTRATION ---
            GroupBox grpSystem = new GroupBox() { Text = "Network Connection Settings", Location = new Point(15, 15), Size = new Size(430, 145) };
            Label lblM = new Label() { Text = "Operation Mode:", Location = new Point(15, 25), Size = new Size(120, 20) };
            cmbMode = new ComboBox() { Location = new Point(15, 45), Size = new Size(400, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMode.Items.AddRange(new object[] { "Master Workstation (Shares local mouse/keyboard out)", "Slave Receiver (Accepts incoming pointer traffic)" });
            cmbMode.SelectedIndex = config.IsMaster ? 0 : 1;
            cmbMode.SelectedIndexChanged += (s, e) => { config.IsMaster = cmbMode.SelectedIndex == 0; config.Save(); };

            Label lblI = new Label() { Text = "Target Destination IP Address (Auto-Discovered if Receiver is on):", Location = new Point(15, 80), Size = new Size(380, 20) };
            txtIP = new TextBox() { Text = config.TargetIP, Location = new Point(15, 100), Size = new Size(400, 25) };
            txtIP.TextChanged += (s, e) => { config.TargetIP = txtIP.Text.Trim(); config.Save(); };
            grpSystem.Controls.Add(lblM); grpSystem.Controls.Add(cmbMode); grpSystem.Controls.Add(lblI); grpSystem.Controls.Add(txtIP);
            Controls.Add(grpSystem);

            // --- DECK 2: MONITOR ARRANGEMENT TOPOLOGY ---
            GroupBox grpGeometry = new GroupBox() { Text = "Monitor Positioning & Grid Topology", Location = new Point(15, 175), Size = new Size(430, 235) };
            Label lblE = new Label() { Text = "Secondary Monitor Location relative to this PC:", Location = new Point(15, 25), Size = new Size(300, 20) };
            cmbEdge = new ComboBox() { Location = new Point(15, 45), Size = new Size(400, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEdge.Items.AddRange(new object[] { "Right Border (Monitor sits on your right side)", "Left Border (Monitor sits on your left side)", "Top Border (Monitor sits above your main rig)", "Bottom Border (Monitor sits beneath your main rig)" });
            cmbEdge.SelectedIndex = config.EdgeDirection;
            cmbEdge.SelectedIndexChanged += (s, e) => { config.EdgeDirection = cmbEdge.SelectedIndex; config.Save(); };

            lblThickness = new Label() { Text = $"Edge Warp Thickness Threshold: {config.EdgeThickness} px", Location = new Point(15, 85), Size = new Size(250, 20) };
            trkThickness = new TrackBar() { Location = new Point(15, 105), Size = new Size(400, 30), Minimum = 1, Maximum = 20, Value = config.EdgeThickness, TickStyle = TickStyle.None };
            trkThickness.Scroll += (s, e) => { config.EdgeThickness = trkThickness.Value; lblThickness.Text = $"Edge Warp Thickness Threshold: {config.EdgeThickness} px"; config.Save(); };

            lblHz = new Label() { Text = $"Transmission Update Refresh Rate: {config.NetworkCadenceHz} Hz", Location = new Point(15, 150), Size = new Size(280, 20) };
            trkHz = new TrackBar() { Location = new Point(15, 170), Size = new Size(400, 30), Minimum = 30, Maximum = 250, Value = config.NetworkCadenceHz, TickStyle = TickStyle.None };
            trkHz.Scroll += (s, e) => { config.NetworkCadenceHz = trkHz.Value; lblHz.Text = $"Transmission Update Refresh Rate: {config.NetworkCadenceHz} Hz"; config.Save(); };
            grpGeometry.Controls.Add(lblE); grpGeometry.Controls.Add(cmbEdge); grpGeometry.Controls.Add(lblThickness); grpGeometry.Controls.Add(trkThickness); grpGeometry.Controls.Add(lblHz); grpGeometry.Controls.Add(trkHz);
            Controls.Add(grpGeometry);

            // --- DECK 3: CRYPTO & ADVANCED MOD SECURITY ---
            GroupBox grpSecurity = new GroupBox() { Text = "Cryptographic Shield Key Allocation", Location = new Point(15, 425), Size = new Size(430, 75) };
            Label lblCrypto = new Label() { Text = "Pre-Shared Local Network Bus Cipher Key (PSK):", Location = new Point(15, 22), Size = new Size(300, 18) };
            txtCryptoKey = new TextBox() { Text = config.SecurityKey, Location = new Point(15, 42), Size = new Size(400, 25), PasswordChar = '●' };
            txtCryptoKey.TextChanged += (s, e) => { config.SecurityKey = txtCryptoKey.Text; config.Save(); };
            grpSecurity.Controls.Add(lblCrypto); grpSecurity.Controls.Add(txtCryptoKey);
            Controls.Add(grpSecurity);

            chkInvert = new CheckBox() { Text = "Mod: Invert Remote Mouse Coordinate Axises", Checked = config.InvertMouse, Location = new Point(25, 510), Size = new Size(350, 20) };
            chkInvert.CheckedChanged += (s, e) => { config.InvertMouse = chkInvert.Checked; config.Save(); };

            chkMute = new CheckBox() { Text = "Optimization: Disable Log Box Output (Saves CPU)", Checked = config.MuteLogs, Location = new Point(25, 532), Size = new Size(350, 20) };
            chkMute.CheckedChanged += (s, e) => { config.MuteLogs = chkMute.Checked; config.Save(); };
            Controls.Add(chkInvert); Controls.Add(chkMute);

            lstLog = new ListBox() { Location = new Point(15, 560), Size = new Size(430, 80) };
            Controls.Add(lstLog);

            btnEngage = new Button() { Text = "Start OpenKVM Shield Engine", Location = new Point(15, 650), Size = new Size(430, 40) };
            Controls.Add(btnEngage);

            edgeWatchTimer = new System.Windows.Forms.Timer() { Interval = 10 };
            edgeWatchTimer.Tick += MasterEdgeGuardCheck;

            btnEngage.Click += (s, e) => {
                if (!isRunning) {
                    isRunning = true;
                    cmbMode.Enabled = cmbEdge.Enabled = txtIP.Enabled = trkThickness.Enabled = trkHz.Enabled = chkInvert.Enabled = txtCryptoKey.Enabled = false;
                    btnEngage.Text = "Stop OpenKVM Shield Loop";
                    if (config.IsMaster) LaunchMasterPipeline();
                    else LaunchSlavePipeline();
                } else {
                    CleanShutdown();
                    cmbMode.Enabled = cmbEdge.Enabled = txtIP.Enabled = trkThickness.Enabled = trkHz.Enabled = chkInvert.Enabled = txtCryptoKey.Enabled = true;
                    btnEngage.Text = "Start OpenKVM Shield Engine";
                }
            };

            this.FormClosing += KVMForm_FormClosing;
            LaunchDiscoverySubsystem();
            Log("OpenKVM v5.0 Shield Core initialized under secure Admin verification.");
        }

        private void Log(string msg) {
            if (config.MuteLogs) return;
            string formatted = $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}";
            if (this.InvokeRequired) {
                this.BeginInvoke(new Action(() => { lstLog.Items.Add(formatted); lstLog.SelectedIndex = lstLog.Items.Count - 1; }));
            } else {
                lstLog.Items.Add(formatted); lstLog.SelectedIndex = lstLog.Items.Count - 1;
            }
        }

        private void KVMForm_FormClosing(object? sender, FormClosingEventArgs e) {
            if (e.CloseReason == CloseReason.UserClosing && isRunning) {
                e.Cancel = true; this.Hide();
                // FIXED: Changed ToolTipIcon.Shield to ToolTipIcon.Info
                trayIcon.ShowBalloonTip(1500, "OpenKVM Background Operation Mode Enabled", "Bus hooks remain active in the system tray layer.", ToolTipIcon.Info);
            }
        }

        // ========================================================================
        // 3. CRYPTOGRAPHIC SYMMETRIC STREAM PIPELINE (Allocation-Free)
        // ========================================================================
        private static void CryptPayloadInline(byte[] buffer, int length, string key) {
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 1; i < length; i++) {
                buffer[i] ^= (byte)(key[i % key.Length] ^ (i * 17));
            }
        }

        // ========================================================================
        // 4. NETWORK BEACON DISCOVERY ARCHITECTURE
        // ========================================================================
        private void LaunchDiscoverySubsystem() {
            discoveryThread = new Thread(() => {
                if (config.IsMaster) {
                    UdpClient discoveryServer = new UdpClient(5558);
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    while (!this.IsDisposed) {
                        try {
                            byte[] payload = discoveryServer.Receive(ref remoteEP);
                            string packetMsg = Encoding.UTF8.GetString(payload);
                            if (packetMsg.StartsWith("OPENKVM_SLAVE_BEACON") && !isRunning) {
                                string discoveredSlaveIP = remoteEP.Address.ToString();
                                if (config.TargetIP != discoveredSlaveIP) {
                                    config.TargetIP = discoveredSlaveIP;
                                    Invoke(new Action(() => { txtIP.Text = discoveredSlaveIP; }));
                                    Log($"Auto-Discovery verified Target Destination Node: {discoveredSlaveIP}");
                                }
                            }
                        } catch { break; }
                    }
                    discoveryServer.Close();
                } else {
                    discoveryBeaconClient = new UdpClient();
                    byte[] beaconToken = Encoding.UTF8.GetBytes("OPENKVM_SLAVE_BEACON_ACTIVE");
                    IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 5558);
                    discoveryBeaconClient.EnableBroadcast = true;
                    while (!this.IsDisposed) {
                        try {
                            if (isRunning) discoveryBeaconClient.Send(beaconToken, beaconToken.Length, broadcastEP);
                            Thread.Sleep(3000);
                        } catch { break; }
                    }
                }
            }) { IsBackground = true };
            discoveryThread.Start();
        }

        // ========================================================================
        // 5. FILE TRANSFER MANAGEMENT LAYERS
        // ========================================================================
        private void KVMForm_DragEnter(object? sender, DragEventArgs e) {
            if (!config.IsMaster || !isRunning) { e.Effect = DragDropEffects.None; return; }
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void KVMForm_DragDrop(object? sender, DragEventArgs e) {
            if (e.Data == null) return;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length == 0) return;
            string targetedFilePath = files[0];
            Log($"Streaming encrypted binary file manifestation: {Path.GetFileName(targetedFilePath)}");

            ThreadPool.QueueUserWorkItem((state) => {
                try {
                    using (TcpClient tcpClient = new TcpClient()) {
                        tcpClient.Connect(config.TargetIP, 5557);
                        using (NetworkStream netStream = tcpClient.GetStream())
                        using (BinaryWriter writer = new BinaryWriter(netStream)) {
                            byte[] nameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(targetedFilePath));
                            writer.Write(nameBytes.Length); writer.Write(nameBytes);
                            byte[] rawFilePayload = File.ReadAllBytes(targetedFilePath);
                            
                            CryptPayloadInline(rawFilePayload, rawFilePayload.Length, config.SecurityKey);
                            writer.Write(rawFilePayload.LongLength); writer.Write(rawFilePayload);
                            writer.Flush();
                        }
                    }
                    Log($"Secure file stream execution completed successfully.");
                } catch (Exception ex) { Log($"FILE ROUTING INTERRUPT: {ex.Message}"); }
            });
        }

        // ========================================================================
        // 6. MASTER BUS HOOK & NETWORK WATCHDOG SUBORDINATE PIPELINES
        // ========================================================================
        private void LaunchMasterPipeline() {
            Log($"Allocating core system hooks pointing to target route: {config.TargetIP}");
            lastSlaveHeartbeatTimestamp = DateTime.Now; 
            try {
                udpSender = new UdpClient();
                sendEndPoint = new IPEndPoint(IPAddress.Parse(config.TargetIP), 5555);
                mouseHookProcedure = MasterMouseHookCallback;
                keyboardHookProcedure = MasterKeyboardHookCallback;

                using (Process current = Process.GetCurrentProcess())
                using (ProcessModule? module = current.MainModule) {
                    IntPtr modHandle = GetModuleHandle(module!.ModuleName!);
                    mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, mouseHookProcedure, modHandle, 0);
                    keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookProcedure, modHandle, 0);
                }
                edgeWatchTimer.Start();
                Log("Global low-level OS hooks initialized.");
            } catch (Exception ex) { Log($"HOOK ACQUISITION REJECTED: {ex.Message}"); }

            listenerThread = new Thread(() => {
                UdpClient receiver = new UdpClient(5556);
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (isRunning) {
                    try {
                        byte[] bytes = receiver.Receive(ref remoteEP);
                        if (bytes.Length <= 0) continue;
                        
                        CryptPayloadInline(bytes, bytes.Length, config.SecurityKey);

                        if (bytes[0] == 0x98) {
                            lastSlaveHeartbeatTimestamp = DateTime.Now;
                            continue;
                        }

                        if (bytes[0] == 0x99) { 
                            remoteMode = false;
                            Invoke(new Action(() => {
                                if (Screen.PrimaryScreen != null) {
                                    if (config.EdgeDirection == 0) Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Width - 100, Screen.PrimaryScreen.Bounds.Height / 2);
                                    else if (config.EdgeDirection == 1) Cursor.Position = new Point(100, Screen.PrimaryScreen.Bounds.Height / 2);
                                    else if (config.EdgeDirection == 2) Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Width / 2, 100);
                                    else Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height - 100);
                                }
                                Log("Target issued release handshakes. Reclaiming pointer focus.");
                            }));
                        }
                    } catch { }
                }
                receiver.Close();
            }) { IsBackground = true };
            listenerThread.Start();
        }

        private void MasterEdgeGuardCheck(object? sender, EventArgs e) {
            if (Screen.PrimaryScreen == null) return;

            if (remoteMode && (DateTime.Now - lastSlaveHeartbeatTimestamp).TotalMilliseconds > 1500) {
                remoteMode = false;
                Invoke(new Action(() => {
                    Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2);
                    Log("CRITICAL DETACH: Target node failed to heartbeat! Enforcing fail-safe context return.");
                }));
                return;
            }

            if (remoteMode) return;

            Point currentPos = Cursor.Position;
            int width = Screen.PrimaryScreen.Bounds.Width; int height = Screen.PrimaryScreen.Bounds.Height;
            int thick = config.EdgeThickness;

            bool breached = false;
            if (config.EdgeDirection == 0 && currentPos.X >= width - thick) breached = true;
            else if (config.EdgeDirection == 1 && currentPos.X <= thick) breached = true;
            else if (config.EdgeDirection == 2 && currentPos.Y <= thick) breached = true;
            else if (config.EdgeDirection == 3 && currentPos.Y >= height - thick) breached = true;

            if (breached) {
                remoteMode = true; lastSlaveHeartbeatTimestamp = DateTime.Now; 
                Log("Grid perimeter breached. Syncing clipboards and projecting absolute metrics...");

                string localClipboard = Clipboard.GetText();
                if (!string.IsNullOrEmpty(localClipboard)) {
                    byte[] clipboardBytes = Encoding.UTF8.GetBytes(localClipboard);
                    SendSecureNetworkPacket(0x20, clipboardBytes);
                }

                int normalizedAbsX = (currentPos.X * 65535) / width;
                int normalizedAbsY = (currentPos.Y * 65535) / height;
                SendSecureNetworkPacket(0x01, BitConverter.GetBytes(normalizedAbsX), BitConverter.GetBytes(normalizedAbsY));
            }
        }

        private IntPtr MasterMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && remoteMode && Screen.PrimaryScreen != null) {
                MSLLHOOKSTRUCT mouseStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                byte actionType = 0x00;
                
                if (wParam == (IntPtr)WM_MOUSEMOVE) actionType = 0x01;
                else if (wParam == (IntPtr)WM_LBUTTONDOWN) actionType = 0x02;
                else if (wParam == (IntPtr)WM_LBUTTONUP) actionType = 0x03;
                else if (wParam == (IntPtr)WM_RBUTTONDOWN) actionType = 0x04;
                else if (wParam == (IntPtr)WM_RBUTTONUP) actionType = 0x05;

                if (actionType != 0x00) {
                    int targetX = mouseStruct.pt.x; int targetY = mouseStruct.pt.y;
                    if (config.InvertMouse && actionType == 0x01) targetX = Screen.PrimaryScreen.Bounds.Width - targetX;

                    int normalizedAbsX = (targetX * 65535) / Screen.PrimaryScreen.Bounds.Width;
                    int normalizedAbsY = (targetY * 65535) / Screen.PrimaryScreen.Bounds.Height;

                    SendSecureNetworkPacket(actionType, BitConverter.GetBytes(normalizedAbsX), BitConverter.GetBytes(normalizedAbsY));
                    int delayMs = 1000 / config.NetworkCadenceHz;
                    if (delayMs > 0) Thread.Sleep(delayMs);
                    return (IntPtr)1; 
                }
            }
            return CallNextHookEx(mouseHookID, nCode, wParam, lParam);
        }

        private IntPtr MasterKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && remoteMode) {
                KBDLLHOOKSTRUCT kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                byte actionType = 0x00;
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN) actionType = 0x10;
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP) actionType = 0x11;

                if (actionType != 0x00) {
                    SendSecureNetworkPacket(actionType, BitConverter.GetBytes(kbStruct.vkCode), BitConverter.GetBytes(kbStruct.scanCode));
                    return (IntPtr)1; 
                }
            }
            return CallNextHookEx(keyboardHookID, nCode, wParam, lParam);
        }

        private void SendSecureNetworkPacket(byte opCode, byte[] dataA, byte[]? dataB = null) {
            if (udpSender == null || sendEndPoint == null) return;
            int payloadSize = 1 + dataA.Length + (dataB?.Length ?? 0);
            byte[] structureBuffer = new byte[payloadSize];
            
            structureBuffer[0] = opCode;
            Buffer.BlockCopy(dataA, 0, structureBuffer, 1, dataA.Length);
            if (dataB != null) Buffer.BlockCopy(dataB, 0, structureBuffer, 1 + dataA.Length, dataB.Length);

            CryptPayloadInline(structureBuffer, structureBuffer.Length, config.SecurityKey);
            try { udpSender.Send(structureBuffer, structureBuffer.Length, sendEndPoint); } catch { }
        }

        // ========================================================================
        // 7. SLAVE RECEIVER PIPELINES & INTERACTION DRIVERS
        // ========================================================================
        private void LaunchSlavePipeline() {
            Log("Opening secure system boundary UDP metrics receiver port 5555...");
            listenerThread = new Thread(() => {
                UdpClient receiver = new UdpClient(5555);
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (isRunning) {
                    try {
                        byte[] bytes = receiver.Receive(ref remoteEP);
                        if (bytes.Length < 5 || Screen.PrimaryScreen == null) continue;

                        CryptPayloadInline(bytes, bytes.Length, config.SecurityKey);
                        byte operationCode = bytes[0];

                        if (operationCode == 0x20) {
                            string sharedString = Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1);
                            Invoke(new Action(() => { try { Clipboard.SetText(sharedString); } catch { } }));
                            continue;
                        }

                        if (operationCode == 0x10 || operationCode == 0x11) {
                            uint vkCode = BitConverter.ToUInt32(bytes, 1); uint scanCode = BitConverter.ToUInt32(bytes, 5);
                            uint flags = (operationCode == 0x11) ? 0x0002u : 0u;
                            keybd_event((byte)vkCode, (byte)scanCode, flags, UIntPtr.Zero);
                            continue;
                        }

                        int incomingAbsX = BitConverter.ToInt32(bytes, 1); int incomingAbsY = BitConverter.ToInt32(bytes, 5);
                        int localX = (incomingAbsX * Screen.PrimaryScreen.Bounds.Width) / 65535;
                        int localY = (incomingAbsY * Screen.PrimaryScreen.Bounds.Height) / 65535;

                        int widthBounds = Screen.PrimaryScreen.Bounds.Width; int heightBounds = Screen.PrimaryScreen.Bounds.Height;
                        bool exitTriggered = false;
                        if (config.EdgeDirection == 0 && localX < -15) exitTriggered = true;
                        else if (config.EdgeDirection == 1 && localX > widthBounds + 15) exitTriggered = true;
                        else if (config.EdgeDirection == 2 && localY > heightBounds + 15) exitTriggered = true;
                        else if (config.EdgeDirection == 3 && localY < -15) exitTriggered = true;

                        if (exitTriggered) {
                            using (UdpClient exitResponseClient = new UdpClient()) {
                                byte[] releaseToken = new byte[1] { 0x99 };
                                CryptPayloadInline(releaseToken, 1, config.SecurityKey);
                                exitResponseClient.Send(releaseToken, 1, new IPEndPoint(remoteEP.Address, 5556));
                            }
                            continue;
                        }

                        Invoke(new Action(() => {
                            Cursor.Position = new Point(localX, localY);
                            if (operationCode == 0x02) mouse_event(0x0002, 0, 0, 0, 0); if (operationCode == 0x03) mouse_event(0x0004, 0, 0, 0, 0); 
                            if (operationCode == 0x04) mouse_event(0x0008, 0, 0, 0, 0); if (operationCode == 0x05) mouse_event(0x0010, 0, 0, 0, 0); 
                        }));
                    } catch { }
                }
                receiver.Close();
            }) { IsBackground = true };
            listenerThread.Start();

            slaveHeartbeatThread = new Thread(() => {
                using (UdpClient heartbeatClient = new UdpClient()) {
                    byte[] token = new byte[1] { 0x98 };
                    CryptPayloadInline(token, 1, config.SecurityKey);
                    while (isRunning) {
                        try {
                            if (remoteMode || true) { 
                                heartbeatClient.Send(token, 1, new IPEndPoint(IPAddress.Parse(config.TargetIP), 5556));
                            }
                            Thread.Sleep(300);
                        } catch { break; }
                    }
                }
            }) { IsBackground = true };
            slaveHeartbeatThread.Start();

            fileServerThread = new Thread(SlaveFileReceiverServerLoop) { IsBackground = true };
            fileServerThread.Start();
        }

        private void SlaveFileReceiverServerLoop() {
            try {
                fileListener = new TcpListener(IPAddress.Any, 5557); fileListener.Start();
                Log("Secure encrypted file allocation port linked on channel 5557.");
                while (isRunning) {
                    if (!fileListener.Pending()) { Thread.Sleep(100); continue; }
                    using (TcpClient connection = fileListener.AcceptTcpClient())
                    using (NetworkStream netStream = connection.GetStream())
                    using (BinaryReader reader = new BinaryReader(netStream)) {
                        int nameLen = reader.ReadInt32(); byte[] nameBytes = reader.ReadBytes(nameLen);
                        string fileName = Encoding.UTF8.GetString(nameBytes);
                        long paySize = reader.ReadInt64(); byte[] rawFile = reader.ReadBytes((int)paySize);

                        CryptPayloadInline(rawFile, rawFile.Length, config.SecurityKey);
                        string targetDesktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                        File.WriteAllBytes(targetDesktopPath, rawFile);
                        Log($"SECURE TRANSFER COMPLETED: {fileName} saved to local Desktop.");
                    }
                }
            } catch { }
        }

        private void CleanShutdown() {
            isRunning = false; edgeWatchTimer.Stop();
            if (mouseHookID != IntPtr.Zero) UnhookWindowsHookEx(mouseHookID);
            if (keyboardHookID != IntPtr.Zero) UnhookWindowsHookEx(keyboardHookID);
            if (udpSender != null) udpSender.Close(); if (fileListener != null) fileListener.Stop();
            if (discoveryBeaconClient != null) discoveryBeaconClient.Close();
            Log("OpenKVM cryptographic environment collapsed cleanly.");
        }

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }
    }

    // ========================================================================
    // 8. THE MASTER EXECUTION ENVELOPE (PILLAR 1: UAC ESCALATION)
    // ========================================================================
    public class Program
    {
        private static bool VerifyAdminPrivilegePrivileges() {
            using (WindowsIdentity userToken = WindowsIdentity.GetCurrent()) {
                WindowsPrincipal securityContext = new WindowsPrincipal(userToken);
                return securityContext.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!VerifyAdminPrivilegePrivileges()) {
                ProcessStartInfo escalationBlueprint = new ProcessStartInfo {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = true,
                    Verb = "runas" 
                };
                try {
                    Process.Start(escalationBlueprint);
                } catch {
                    MessageBox.Show("CRITICAL FAULT: OpenKVM require full Administrator privileges to intercept low-level mouse and keyboard hooks.", "UAC Escalation Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Application.Exit();
                return;
            }

            Application.Run(new KVMForm());
        }
    }
}