# TouchpadGestureControl

TouchpadGestureControl is a lightweight, low-latency Windows background utility that translates continuous three-finger rotational gestures on precision touchpads into fine-grained system audio volume adjustments.

---

## Abstract and Problem Formulation

Modern Windows operating systems (Windows 10 and Windows 11) implement native gesture interception within the Precision Touchpad (PTP) subsystem. Multi-finger motions—such as three-finger horizontal and vertical swipes—are captured at the driver and shell level to trigger window management actions (e.g., Task View, virtual desktop switching, and application switching). Consequently, standard application window message queues (`WM_POINTER`, `WM_TOUCH`, `WM_GESTURE`) are bypassed or suppressed when multi-finger gestures occur.

TouchpadGestureControl resolves this architectural limitation by tapping directly into the Windows Raw Input subsystem (`WM_INPUT`) with background sink flags (`RIDEV_INPUTSINK`). By parsing raw Human Interface Device (HID) digitizer reports prior to operating system gesture classification, the application extracts low-level contact telemetry ($X, Y$ coordinates, contact identifiers, tip switch states) and performs real-time kinematic rotation analysis.

---

## System Architecture

The application is structured into four decoupled layers:

```
+-------------------------------------------------------------------+
|                        Hardware Layer                             |
|           Precision Touchpad / HID Digitizer (0x000D, 0x0005)      |
+---------------------------------+---------------------------------+
                                  |
                                  v (Raw HID Packets)
+-------------------------------------------------------------------+
|                         Input Subsystem                           |
|  - RawHidProvider (WM_INPUT + HidP_* Parser) [Primary]            |
|  - WmPointerProvider (RegisterPointerInputTarget) [Fallback 1]    |
|  - WmTouchProvider (Layered Transparent Overlay) [Fallback 2]    |
+---------------------------------+---------------------------------+
                                  |
                                  v (Normalized TouchFrame)
+-------------------------------------------------------------------+
|                         Gesture Engine                            |
|  - Dynamic Centroid Calculation                                   |
|  - Relative Angular Displacement & Circular Slot Matching         |
|  - Exponential Moving Average (EMA) Filtering                     |
|  - Deadzone Integration & Micro-Dropout Tolerance                |
+---------------------------------+---------------------------------+
                                  |
                                  v (Quantized Volume Delta)
+-------------------------------------------------------------------+
|                         Audio & UI Layer                          |
|  - Core Audio COM Endpoint (IAudioEndpointVolume)                 |
|  - Real-Time Diagnostic Dashboard & Surface Radar (WinForms)      |
|  - System Notification Area (Tray) Loop                           |
+-------------------------------------------------------------------+
```

---

## Algorithmic Formulation

### 1. Coordinate Normalization
Incoming digitizer values from top-level HID collections are sampled in device-specific logical units. These coordinates are mapped to a normalized two-dimensional Cartesian plane:

$$x_{\text{norm}} = \frac{x_{\text{raw}}}{X_{\text{max}}} \cdot W_{\text{target}}, \quad y_{\text{norm}} = \frac{y_{\text{raw}}}{Y_{\text{max}}} \cdot H_{\text{target}}$$

### 2. Dynamic Centroid Computation
For a set of $N$ active contact points $P = \{ (x_1, y_1), (x_2, y_2), \dots, (x_N, y_N) \}$ where $N \ge 3$, the centroid $C = (\bar{x}, \bar{y})$ is computed as the geometric mean:

$$\bar{x} = \frac{1}{N} \sum_{i=1}^{N} x_i, \quad \bar{y} = \frac{1}{N} \sum_{i=1}^{N} y_i$$

### 3. Contact Angle Determination
The angular position $\theta_i$ of each contact relative to the centroid is calculated via four-quadrant arctangent:

$$\theta_i = \mathrm{atan2}(y_i - \bar{y}, x_i - \bar{x}), \quad \theta_i \in (-\pi, \pi]$$

Because Windows screen space defines the Y-axis as increasing downwards, clockwise angular displacement corresponds to a positive delta in visual screen space.

### 4. Wrap-Safe Angular Delta and Circular Slot Matching
For consecutive frames $t-1$ and $t$, the per-contact angular displacement $\delta\theta_i$ is computed using trigonometric normalization to handle $(-\pi, \pi]$ boundary crossings:

$$\delta\theta_i = \mathrm{atan2}\left(\sin(\theta_{i,t} - \theta_{i,t-1}), \cos(\theta_{i,t} - \theta_{i,t-1})\right)$$

When hardware drivers do not provide persistent contact IDs across frames, the algorithm applies a circular angular slot matching fallback. Contacts are ordered angularly, and slot-to-slot differences are evaluated. The mean rotational delta is:

$$\Delta\theta_{\text{raw}} = \frac{1}{M} \sum_{k=1}^{M} \delta\theta_k, \quad M \ge 2$$

### 5. Jitter Suppression and Exponential Smoothing
Sub-threshold noise from micro-finger oscillations is filtered through a deadband threshold $\epsilon_{\text{jitter}}$:

$$\Delta\theta_{\text{filtered}} = \begin{cases} 0, & |\Delta\theta_{\text{raw}}| < \epsilon_{\text{jitter}} \\ \Delta\theta_{\text{raw}}, & |\Delta\theta_{\text{raw}}| \ge \epsilon_{\text{jitter}} \end{cases}$$

An Exponential Moving Average (EMA) filter smooths the signal without introducing perceptible phase lag:

$$\bar{\Delta\theta}_t = \alpha \cdot \Delta\theta_{\text{filtered}} + (1 - \alpha) \cdot \bar{\Delta\theta}_{t-1}$$

where $\alpha \in (0, 1]$ is the smoothing factor (default: $0.40$).

### 6. Volume Quantization and Rate Limiting
Accumulated angular displacement $\Theta_t = \sum \bar{\Delta\theta}$ triggers discrete volume steps when exceeding the activation threshold $\Theta_{\text{thresh}}$:

$$\text{Steps} = \left\lfloor \frac{\Theta_t}{\Theta_{\text{step}}} \right\rfloor$$

$$\Delta V = \text{Steps} \cdot V_{\text{step}}$$

$$\Theta_{\text{remainder}} = \Theta_t - (\text{Steps} \cdot \Theta_{\text{step}})$$

A sliding-window rate limiter constrains the maximum total volume change per second ($V_{\text{max/sec}}$) to prevent sudden audio surges.

### 7. Micro-Dropout Grace Period
Physical touchpad rotation frequently causes transient contact dropouts lasting 20 to 100 milliseconds. A 250 ms debounce grace timer maintains tracking continuity, preventing state reset during rapid rotational hand repositioning.

---

## Core Audio Integration

System volume manipulation is implemented through the Windows Core Audio COM API:
- `IMMDeviceEnumerator` (`CLSID_MMDeviceEnumerator`) acquires the default audio endpoint (`eRender`, `eMultimedia`).
- `IAudioEndpointVolume` provides direct floating-point volume manipulation in the normalized range $[0.0, 1.0]$.
- Changes take effect instantaneously across all active hardware outputs with zero audio pipeline distortion.

---

## Configuration Reference

Runtime parameters can be tuned dynamically via the diagnostic interface or through `Settings.cs`:

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `VolumeStepSize` | `double` | `0.04` (4%) | Volume delta applied per step |
| `DegreesPerVolumeStep` | `double` | `20.0°` | Angular rotation required for one volume step |
| `RotationThresholdDegrees` | `double` | `12.0°` | Deadzone threshold before gesture activation |
| `RotationSmoothingAlpha` | `double` | `0.40` | EMA smoothing weight factor |
| `JitterThresholdDegrees` | `double` | `0.30°` | Angular noise gate per frame |
| `MaxVolumeChangePerSecond` | `double` | `0.40` (40%) | Maximum allowed volume change rate |
| `ClockwiseIsVolumeUp` | `bool` | `true` | Direction mapping (CW = Up, CCW = Down) |
| `PreferredProvider` | `string` | `"Auto"` | Provider priority (`"Auto"`, `"RawHID"`, `"WmPointer"`, `"WmTouch"`) |

---

## Project Structure

```
touchpad/
├── src/
│   └── TouchpadGestureControl/
│       ├── Audio/
│       │   ├── AudioEndpointInterop.cs     # Core Audio COM interfaces
│       │   └── VolumeController.cs         # Windows volume driver
│       ├── Gesture/
│       │   ├── GestureDetector.cs          # State machine and rate limiting
│       │   ├── GestureState.cs             # State enumerations
│       │   └── RotationCalculator.cs       # Geometric and trigonometric math
│       ├── Input/
│       │   ├── ITouchInputProvider.cs      # Provider abstraction
│       │   ├── RawHidProvider.cs           # Raw Input (WM_INPUT) implementation
│       │   ├── TouchFrame.cs               # Contact snapshot structs
│       │   ├── WmPointerProvider.cs        # WM_POINTER provider
│       │   └── WmTouchProvider.cs          # WM_TOUCH provider
│       ├── NativeApi/
│       │   ├── NativeConstants.cs          # Win32 and HID constants
│       │   ├── NativeMethods.cs            # P/Invoke declarations
│       │   └── NativeStructs.cs            # Win32 struct definitions
│       ├── UI/
│       │   ├── DiagnosticForm.cs           # Diagnostic dashboard & live tuner
│       │   ├── MessageWindow.cs            # Hidden message pump window
│       │   └── TrayApplication.cs          # System tray lifecycle controller
│       ├── Program.cs                      # Entry point & single-instance mutex
│       ├── Settings.cs                     # Central configuration constants
│       └── TouchpadGestureControl.csproj   # Project file (.NET 8.0-windows)
├── tests/
│   └── GestureTests/
│       ├── RotationAlgorithmTests.cs       # 19 unit tests for gesture kinematics
│       └── GestureTests.csproj             # Test project file
├── README.md                               # Technical documentation
└── TouchpadGestureControl.sln              # Solution configuration
```

---

## Prerequisites and Compilation

### Requirements
- Operating System: Windows 10 (Build 1809+) or Windows 11
- Hardware: Precision Touchpad (HID Digitizer compliant)
- Runtime: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- SDK: .NET 8.0 SDK (for building from source)

### Building from Source

Clone the repository and build the Release configuration:

```powershell
# Restore dependencies and build solution
dotnet build -c Release
```

### Running the Application

```powershell
# Standard background mode (system tray only)
dotnet run --project src\TouchpadGestureControl\TouchpadGestureControl.csproj -c Release

# Diagnostic mode (opens telemetry dashboard with live touch radar)
dotnet run --project src\TouchpadGestureControl\TouchpadGestureControl.csproj -c Release -- --diagnostic
```

Alternatively, invoke the compiled binary directly:

```powershell
& "src\TouchpadGestureControl\bin\Release\net8.0-windows\win-x64\TouchpadGestureControl.exe" --diagnostic
```

---

## Verification and Automated Testing

The gesture engine is validated through 19 unit tests operating on synthetic coordinate arrays without hardware dependencies:

```powershell
dotnet test tests\GestureTests\GestureTests.csproj -v normal
```

Test coverage includes:
- Monotonic clockwise and counter-clockwise rotation accuracy.
- Multi-quadrant wrap-around $(-\pi \leftrightarrow \pi)$ continuity.
- Pure translational movement rejection (zero false-positive volume triggers).
- High-frequency jitter noise suppression.
- Circular slot matching under randomized and unstable hardware contact IDs.
- Micro-dropout tolerance and centroid calculation precision.

---

## License

Distributed under the MIT License. See `LICENSE` for additional details.
