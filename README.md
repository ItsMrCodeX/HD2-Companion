# HD2 Stratagem Deck

A companion app for Helldivers 2 that lets you launch stratagems from your mobile device to your PC. Built with a mobile interface (MAUI) and a lightweight desktop server that emulates keyboard input.

## What it does

- **Desktop Server (`StratagemDeck.Server`)**: Listens for UDP commands on port `12345`, simulates keyboard input (`Ctrl + arrow sequences`) using Windows `SendInput` API
- **Mobile App (`StratagemDeck.Mobile`)**: 3-tab UI (Game / Setup / Settings) for building loadouts and sending stratagems with a single tap
- **QR Code pairing**: Server displays a QR code with IP + PIN — scan it with the mobile app for instant connection
- **Active Network Scan**: Mobile scans the local subnet to discover the server automatically

## Technologies

| Component | Stack |
|---|---|
| Mobile App | .NET 10 MAUI (Android, iOS) |
| Desktop Server | .NET 9 Console App (Windows only) |
| Communication | UDP |
| QR Generation | QRCoder |
| QR Scanning | ZXing.Net.Maui.Controls |
| Data | JSON (embedded `stratagems.json`) |
| Icon Rendering | SkiaSharp + Svg.Skia (SVG to PNG with disk cache) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for the MAUI app)
- .NET 9 runtime (included with SDK, for the server)
- MAUI workload: `dotnet workload install maui`
- Android: Android SDK 21+ (for building the APK)
- Windows: Windows 10+ (for running the server, `SendInput` API)

## How to run

### 1. Clone

```bash
git clone https://github.com/ItsMrCodeX/HellDivers2-Stratagem-Deck
cd HellDivers2-Stratagem-Deck
```

### 2. Restore dependencies

```bash
dotnet restore StratagemDeck.slnx
```

### 3. Run the Server on your PC

```bash
dotnet run --project StratagemDeck.Server
```

The console will display:
- Your local IP addresses
- A **4-digit PIN**
- An ASCII **QR code** containing `IP:PIN`

Keep this window open while playing.

### 4. Build & deploy the Mobile App

**Android:**
```bash
dotnet build StratagemDeck.Mobile -f net10.0-android -c Release
# APK: StratagemDeck.Mobile/bin/Release/net10.0-android/
```

**Windows (for testing):**
```bash
dotnet build StratagemDeck.Mobile -f net10.0-windows10.0.19041.0
dotnet run --project StratagemDeck.Mobile -f net10.0-windows10.0.19041.0
```

### 5. Connect

**Option A — QR Code:**
1. Open the app → Settings tab → tap **Scan QR Code**
2. Point camera at the server's console QR code
3. App auto-connects

**Option B — Manual:**
1. Settings → Manual Entry → enter server IP and PIN → Connect

### 6. Play

- **Setup tab**: Assign 4 stratagems to your loadout + mission stratagems. Use the **search bar** to quickly find stratagems by name or short name.
- **Game tab**: Tap any stratagem to send it. Tap Mission stratagems to send those too.
- **Save** your loadout, **Clear** to reset.

## Project structure

```
HellDivers2-Stratagem-Deck/
├── StratagemDeck.slnx
├── StratagemDeck.Server/          # .NET 9 Console (Windows)
│   ├── Program.cs                # Entry point, QR display
│   ├── Native/KeyInjector.cs     # SendInput P/Invoke
│   └── Services/
│       ├── CommandListener.cs    # UDP listener
│       └── PinManager.cs         # 4-digit PIN
├── StratagemDeck.Mobile/          # .NET 10 MAUI
│   ├── Pages/
│   │   ├── GamePage.xaml         # In-game quick send
│   │   ├── SetupPage.xaml        # Loadout builder
│   │   ├── SettingsPage.xaml     # Connection & PIN
│   │   └── QrScanPage.xaml       # QR scanner
│   ├── ViewModels/
│   ├── Services/
│   ├── Models/
│       └── Resources/Raw/
│           ├── stratagems.json       # 124 stratagems with keys + shortName
│           └── icons/                # SVG icons
├── README.md
└── .gitignore
```

## Notes

- The server must run on the **same local network** as the mobile device
- Windows Firewall may block UDP port 12345 — allow it on first run
- For best QR scanning, maximize the console window and use good lighting
- Stratagem data is embedded in the app — update `stratagems.json` and `icons/` for game patches
