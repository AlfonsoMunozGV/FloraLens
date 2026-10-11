# Flower Image Classifier

A .NET MAUI application for identifying flower images and comparing the result with a flower name spoken by the user. The app combines a custom-trained Azure Custom Vision model with speech recognition and a simple, touch-friendly image viewer.

> **Security:** Real credentials belong in the ignored `FloraLens/appsettings.local.json`, never in `appsettings.json` or source control. The local override is included only in Debug builds for development and can be extracted from those builds. Release builds exclude it. A client app cannot keep a bundled API key secret; use a server-side proxy for any distributed build. Rotate any key that has been committed or otherwise exposed.

## Screenshots

Add screenshots under `docs/images/` and replace these placeholders with the corresponding files.

<!-- Screenshot placeholder: main screen before selecting an image. -->
![Main screen](/mainEmpty.png)

<!-- Screenshot placeholder: selected flower image and voice input. -->
![Flower image and voice input](/main.png)

<!-- Screenshot placeholder: comparison between the spoken answer and Azure prediction. -->
![Prediction comparison](/result.png)

## Features

- Capture a flower photo with the device camera or choose one from the gallery.
- Zoom and pan around the selected image.
- Say a flower name and compare it with the model prediction.
- Classify the image with an Azure Custom Vision prediction endpoint.
- Show the top predictions and confidence values returned by the model.
- Use native, on-device Spanish flower-name recognition on Windows when the Spanish speech grammar is installed.

## Technology

- .NET 10 and .NET MAUI 10
- C# and XAML
- Azure Custom Vision Prediction API
- CommunityToolkit.Maui, including speech-to-text support on non-Windows targets
- Windows `SpeechRecognizer` with a local list grammar for supported Windows builds

## Requirements

- .NET 10 SDK
- .NET MAUI workload for the target platform
- An Azure Custom Vision project with a published image-classification iteration
- Windows 10 version 19041 or later for the packaged Windows target
- The Spanish speech language pack on Windows to use the local `es-ES` flower grammar
- A Mac with the required Apple tooling to build or run the iOS and Mac Catalyst targets

## Getting started

1. Clone the repository.
2. Install the .NET MAUI workload for your platform. For example:

   ```bash
   dotnet workload install maui
   ```

3. Create a local settings file and configure Azure Custom Vision as described below. **Do not commit real credentials.**
4. Open `FloraLens.slnx` in Visual Studio or build the project for a supported target framework.

### Azure Custom Vision settings

Copy `FloraLens/appsettings.example.json` to `FloraLens/appsettings.local.json`, then fill in the values for your Azure Custom Vision project:

```json
{
  "AzureCustomVision": {
    "PredictionKey": "YOUR_PREDICTION_KEY",
    "Endpoint": "https://YOUR-RESOURCE.cognitiveservices.azure.com/",
    "ProjectId": "YOUR-PROJECT-GUID",
    "PublishedName": "YOUR-PUBLISHED-ITERATION"
  },
  "SpeechRecognition": {
    "DefaultCulture": "es-ES",
    "EnablePartialResults": true
  }
}
```

`ProjectId` is the Custom Vision project GUID and `PublishedName` is the name of the published model iteration. In Debug, the app loads this ignored file as an embedded resource override. The public `appsettings.json` contains no Azure credentials and provides the default speech settings. The local file is ignored by Git and is not included in Release builds.

**This is a client application:** credentials in a Debug build can be extracted from its package. Use only a development key for local testing. For an app distributed to users, move Custom Vision calls behind a backend that holds the key; do not rely on hiding a key in a MAUI package.

## Build and run

Run commands from the repository root. Replace the framework with one installed on your development machine.

### Windows

```powershell
dotnet build .\FloraLens\FloraLens.csproj `
  -f net10.0-windows10.0.19041.0
```

The Windows target is configured as a packaged MSIX app. In Visual Studio, select the **Windows Machine** launch profile.

### Android

```bash
dotnet build FloraLens/FloraLens.csproj -f net10.0-android
```

### iOS and Mac Catalyst

Build these targets on macOS with the corresponding Apple development tools installed:

```bash
dotnet build FloraLens/FloraLens.csproj -f net10.0-ios
dotnet build FloraLens/FloraLens.csproj -f net10.0-maccatalyst
```

## How to use

1. Select or capture a flower image.
2. Press **Use Microphone** and say one flower name.
3. Press **Analyze with Azure AI** to classify the selected image.
4. Compare the spoken answer with Azure's prediction and confidence.
5. Use **Clear All and Start Again** to reset the screen.

The visible labels in the app are currently in Spanish. On Windows, voice input uses a local list of supported Spanish flower names; add model labels to that list if the classifier uses other names.

## Permissions and privacy

The app can request camera, photo-library, and microphone permissions depending on platform and the feature used. The selected image is sent to the configured Azure Custom Vision endpoint for classification. Review Microsoft's Azure service terms and your deployment's privacy requirements before distributing the app.

## Project layout

```text
FloraLens/
├── Configuration/       # Strongly typed application settings
├── Models/               # Prediction and analysis models
├── Platforms/            # Android, iOS, Mac Catalyst, and Windows configuration
├── Resources/            # Icons, images, fonts, and styles
├── Services/             # Azure Custom Vision service abstraction and client
├── MainPage.xaml         # Main application interface
├── MainPage.xaml.cs      # Image, speech, and analysis interactions
├── appsettings.json      # Safe defaults; no Azure credentials
└── appsettings.example.json # Template to copy to ignored appsettings.local.json
```

## License

This repository is shared under the **FloraLens Evaluation License** in [`LICENSE`](LICENSE). It permits viewing, building, and running an unmodified copy for personal, non-commercial evaluation and testing. It does not permit modifying or redistributing the project or using it commercially. Third-party components keep their own licenses.

The license controls permitted use of the project files; it does not control GitHub access. To keep the official repository unchanged by others, do not add collaborators with write access. People can still fork the public repository and make changes in their own copies, but those changes do not alter your repository. See [GitHub's permission levels for personal repositories](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository).

