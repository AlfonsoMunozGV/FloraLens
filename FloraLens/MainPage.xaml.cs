using CommunityToolkit.Maui.Media;
using FloraLens.Configuration;
using FloraLens.Models;
using FloraLens.Services;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
#if WINDOWS
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
#endif

namespace FloraLens;

public partial class MainPage : ContentPage
{
    private bool _isListening = false;
    private CancellationTokenSource? _cts;
    private string _partialText = "";
    private string _finalText = "";
    private readonly ISpeechToText _speechToText = SpeechToText.Default;
    private readonly ICustomVisionService _customVisionService;
    private readonly SpeechRecognitionSettings _speechSettings;
#if WINDOWS
    private SpeechRecognizer? _windowsSpeechRecognizer;
#endif

    private string _currentImagePath = string.Empty;
    private string _userVoicePrediction = string.Empty;
    private AnalysisResult _currentAnalysis = new();

    // Variables para gestos
    private double _currentScale = 1.0;
    private double _startScale = 1.0;
    private double _lastX, _lastY;
    private double _startX, _startY;
    private const double MIN_SCALE = 1.0;
    private const double MAX_SCALE = 4.0;
    private const double ZOOM_LEVEL_2X = 2.0;

    public MainPage(ICustomVisionService customVisionService, IOptions<SpeechRecognitionSettings> speechSettings)
    {
        InitializeComponent();
        _customVisionService = customVisionService;
        _speechSettings = speechSettings.Value;
    }

    // Botón: Capturar foto con cámara
    private async void OnCameraButtonClicked(object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.IsCaptureSupported)
            {
                await DisplayAlert("Error", "La cámara no está disponible en este dispositivo", "OK");
                return;
            }

            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (cameraStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permiso necesario", "Se necesita permiso de cámara para tomar fotos", "OK");
                return;
            }

            var photo = await MediaPicker.CapturePhotoAsync();

            if (photo != null)
            {
                await LoadImageFromFile(photo.FullPath);
                StatusLabel.Text = "Foto capturada correctamente";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en cámara: {ex.Message}");
            StatusLabel.Text = "Error al capturar foto";
            await DisplayAlert("Error", $"No se pudo capturar la foto: {ex.Message}", "OK");
        }
    }

    // Botón: Seleccionar imagen de galería
    private async void OnGalleryButtonClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (storageStatus != PermissionStatus.Granted)
            {
                storageStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            if (storageStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permiso necesario", "Se necesita permiso para acceder a la galería", "OK");
                return;
            }
#endif

            var photo = await MediaPicker.PickPhotoAsync();

            if (photo != null)
            {
                await LoadImageFromFile(photo.FullPath);
                StatusLabel.Text = "✅ Imagen seleccionada correctamente";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en galería: {ex.Message}");
            StatusLabel.Text = "❌ Error al seleccionar imagen";
            await DisplayAlert("Error", $"No se pudo seleccionar la imagen: {ex.Message}", "OK");
        }
    }

    // Botón: Usar micrófono
    private async void OnMicrophoneButtonClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath))
        {
            await DisplayAlert("Advertencia", "Primero selecciona una imagen", "OK");
            StatusLabel.Text = "Selecciona una imagen primero";
            return;
        }

        if (_isListening)
        {
            await StopListeningAsync();
            return;
        }

        await StartListeningAsync();
    }

    private async Task StartListeningAsync()
    {
        _isListening = true;
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            MicrophoneButton.ImageSource = "microfonoactivo.png";
            MicrophoneButton.Text = "Detener";
            
            _partialText = "";
            _finalText = "";
            _userVoicePrediction = "";
            VoiceResultLabel.Text = "Iniciando reconocimiento...";
            VoiceResultFrame.IsVisible = true;
        });

        try
        {
            // The MAUI microphone permission and the toolkit speech permission are separate checks.
            var microphoneStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (microphoneStatus != PermissionStatus.Granted)
            {
                microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            }

            if (microphoneStatus != PermissionStatus.Granted)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    VoiceResultLabel.Text = "❌ Permiso de micrófono denegado. Actívalo en la configuración de privacidad de Windows.";
                    _isListening = false;
                    MicrophoneButton.ImageSource = "microfonoestudio.png";
                    MicrophoneButton.Text = "Usar Micrófono";
                });
                return;
            }

            var granted = await _speechToText.RequestPermissions();
            if (!granted)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    VoiceResultLabel.Text = "❌ Permiso de micrófono denegado. Ve a Configuración > Apps > Permisos";
                    _isListening = false;
                    MicrophoneButton.ImageSource = "microfonoestudio.png";
                    MicrophoneButton.Text = "Usar Micrófono";
                });
                return;
            }

            _cts = new CancellationTokenSource();

#if WINDOWS
            var culture = CultureInfo.GetCultureInfo(_speechSettings.DefaultCulture);
            await StartWindowsSpeechRecognitionAsync(culture);
#else
            // Subscribe to events BEFORE starting
            _speechToText.RecognitionResultUpdated += OnPartialResult;
            _speechToText.RecognitionResultCompleted += OnFinalResult;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                VoiceResultLabel.Text = "🎤 Escuchando... Habla ahora claramente";
            });

            var culture = CultureInfo.GetCultureInfo(_speechSettings.DefaultCulture);
            await _speechToText.StartListenAsync(
                new SpeechToTextOptions
                {
                    Culture = culture,
                    ShouldReportPartialResults = _speechSettings.EnablePartialResults
                },
                _cts.Token);
#endif

            Debug.WriteLine($"Reconocimiento de voz iniciado con cultura: {culture.Name}");
            
            // The Windows recognizer has its own silence timeouts. A second
            // cancellation timer can race RecognizeAsync and return Status=Unknown.
#if !WINDOWS
            // Add a timeout to auto-stop if no speech detected on other platforms.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
                    if (_isListening && string.IsNullOrEmpty(_partialText) && string.IsNullOrEmpty(_userVoicePrediction))
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            VoiceResultLabel.Text = "⏱️ No se detectó voz. Intenta hablar más cerca del micrófono.";
                        });
                        await StopListeningAsync();
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Timeout check error: {ex.Message}");
                }
            });
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error iniciando reconocimiento de voz: {ex.Message}\n{ex.StackTrace}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                VoiceResultLabel.Text = $"❌ Error: {ex.Message}";
                _isListening = false;
                MicrophoneButton.ImageSource = "microfonoestudio.png";
                MicrophoneButton.Text = "Usar Micrófono";
            });
            
            CleanupEventHandlers();
        }
    }

#if WINDOWS
    private async Task StartWindowsSpeechRecognitionAsync(CultureInfo culture)
    {
        var grammarLanguage = SpeechRecognizer.SupportedGrammarLanguages
            .FirstOrDefault(language => string.Equals(
                language.LanguageTag,
                culture.Name,
                StringComparison.OrdinalIgnoreCase));

        if (grammarLanguage is null)
        {
            var availableLanguages = string.Join(
                ", ",
                SpeechRecognizer.SupportedGrammarLanguages.Select(language => language.LanguageTag));
            throw new InvalidOperationException(
                $"Windows no tiene instalada una gramática de voz para {culture.Name}. " +
                $"Instala el paquete de voz español en Configuración > Hora e idioma > Idioma y región. " +
                (string.IsNullOrWhiteSpace(availableLanguages)
                    ? "No hay idiomas de reconocimiento sin conexión instalados."
                    : $"Idiomas disponibles: {availableLanguages}.") );
        }

        var recognizer = new SpeechRecognizer(new Language(culture.Name));
        recognizer.HypothesisGenerated += OnWindowsHypothesisGenerated;
        recognizer.RecognitionQualityDegrading += OnWindowsRecognitionQualityDegrading;

        try
        {
            // This app expects a single flower name. A local list grammar is more
            // accurate for short, known phrases and does not depend on online dictation.
            recognizer.Constraints.Add(new SpeechRecognitionListConstraint(
                FlowerNames,
                "flower-name"));

            var compilation = await recognizer.CompileConstraintsAsync();
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException(
                    $"Windows no puede preparar la gramática de flores en {culture.Name}: {compilation.Status}.");
            }

            recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(20);
            recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.5);
            recognizer.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(10);

            _windowsSpeechRecognizer = recognizer;
            _ = RecognizeWindowsPhraseAsync(recognizer);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                VoiceResultLabel.Text = $"🎤 Escuchando en {grammarLanguage.LanguageTag}… Di un nombre de flor en español.";
            });
        }
        catch
        {
            recognizer.HypothesisGenerated -= OnWindowsHypothesisGenerated;
            recognizer.RecognitionQualityDegrading -= OnWindowsRecognitionQualityDegrading;
            recognizer.Dispose();
            throw;
        }
    }

    private static readonly string[] FlowerNames =
    [
        "abutilón", "amapola", "azalea", "azucena", "begonia", "belladona", "buganvilla",
        "caléndula", "campanilla", "clavel", "ciclamen", "crisantemo", "dalia", "diente de león",
        "edelweiss", "flor de loto", "gardenia", "geranio", "gerbera", "girasol", "gladiolo",
        "hortensia", "jazmín", "lavanda", "lila", "lirio", "loto", "magnolia", "margarita",
        "narciso", "nomeolvides", "orquídea", "pensamiento", "peonía", "petunia", "prímula",
        "ranúnculo", "retama", "rosa", "rosal", "sakura", "tulipán", "verbena", "violeta",
        "alhelí", "anturio", "aster", "boca de dragón", "brezo", "cala", "camelia", "celosía",
        "dondiego", "fresia", "hibisco", "iris", "jacinto", "malva", "nardo", "pasiflora",
        "santolina", "siempreviva", "tagete", "alcatraz", "dragonaria", "flor de pascua"
    ];

    private async Task RecognizeWindowsPhraseAsync(SpeechRecognizer recognizer)
    {
        var wasActiveRecognizer = false;
        try
        {
            var result = await recognizer.RecognizeAsync();
            wasActiveRecognizer = ReferenceEquals(_windowsSpeechRecognizer, recognizer);
            if (wasActiveRecognizer)
            {
                _windowsSpeechRecognizer = null;
            }

            ProcessWindowsRecognitionResult(result);
        }
        catch (Exception ex)
        {
            wasActiveRecognizer = ReferenceEquals(_windowsSpeechRecognizer, recognizer);
            if (wasActiveRecognizer)
            {
                _windowsSpeechRecognizer = null;
                Debug.WriteLine($"Error del reconocedor nativo de Windows: {ex}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    VoiceResultLabel.Text = $"❌ Windows no pudo reconocer la voz: {ex.GetBaseException().Message}";
                });
            }
        }
        finally
        {
            recognizer.HypothesisGenerated -= OnWindowsHypothesisGenerated;
            recognizer.RecognitionQualityDegrading -= OnWindowsRecognitionQualityDegrading;
            recognizer.Dispose();

            if (wasActiveRecognizer)
            {
                _isListening = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MicrophoneButton.ImageSource = "microfonoestudio.png";
                    MicrophoneButton.Text = "Usar Micrófono";
                });
                _cts?.Cancel();
            }
        }
    }

    private void OnWindowsHypothesisGenerated(
        SpeechRecognizer sender,
        SpeechRecognitionHypothesisGeneratedEventArgs args)
    {
        var hypothesis = args.Hypothesis.Text?.Trim();
        if (string.IsNullOrWhiteSpace(hypothesis))
        {
            return;
        }

        _partialText = hypothesis;
        MainThread.BeginInvokeOnMainThread(() => VoiceResultLabel.Text = $"Escuchando: {hypothesis}");
    }

    private void OnWindowsRecognitionQualityDegrading(
        SpeechRecognizer sender,
        SpeechRecognitionQualityDegradingEventArgs args)
    {
        var detail = args.Problem switch
        {
            SpeechRecognitionAudioProblem.NoSignal => "Windows no recibe señal del micrófono. Revisa el dispositivo y el medidor en Configuración > Sistema > Sonido > Entrada.",
            SpeechRecognitionAudioProblem.TooQuiet => "Windows recibe el micrófono con volumen demasiado bajo. Sube el nivel de entrada en Configuración > Sistema > Sonido > Entrada.",
            SpeechRecognitionAudioProblem.TooLoud => "La entrada está saturada. Baja el nivel del micrófono y habla con volumen normal.",
            SpeechRecognitionAudioProblem.TooNoisy => "Hay demasiado ruido de fondo para reconocer la voz.",
            SpeechRecognitionAudioProblem.TooFast => "Habla más despacio.",
            SpeechRecognitionAudioProblem.TooSlow => "Habla a un ritmo normal.",
            _ => $"Windows detectó un problema de audio: {args.Problem}."
        };

        Debug.WriteLine($"Calidad de audio del reconocimiento: {args.Problem}");
        MainThread.BeginInvokeOnMainThread(() => VoiceResultLabel.Text = $"⚠️ {detail}");
    }

    private void ProcessWindowsRecognitionResult(SpeechRecognitionResult result)
    {
        var text = result.Text?.Trim() ?? string.Empty;
        Debug.WriteLine($"Resultado de voz Windows: Status={result.Status}, Text='{text}'");

        if (result.Status != SpeechRecognitionResultStatus.Success)
        {
            var detail = result.Status switch
            {
                SpeechRecognitionResultStatus.AudioQualityFailure => "Windows detectó un problema con la señal del micrófono.",
                SpeechRecognitionResultStatus.TimeoutExceeded => "Windows no detectó una frase antes del tiempo límite.",
                SpeechRecognitionResultStatus.TopicLanguageNotSupported => $"Windows no admite el idioma configurado ({_speechSettings.DefaultCulture}) para el reconocimiento en línea.",
                SpeechRecognitionResultStatus.Unknown => "Windows terminó sin confirmar una palabra. Pulsa el micrófono para reintentar y di una sola flor, por ejemplo: rosa, margarita, girasol o tulipán.",
                _ => $"Windows terminó el reconocimiento con estado {result.Status}."
            };

            MainThread.BeginInvokeOnMainThread(() => VoiceResultLabel.Text = $"⚠️ {detail}");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            MainThread.BeginInvokeOnMainThread(() =>
                VoiceResultLabel.Text = "No se reconoció el nombre de la flor. Comprueba que Windows esté usando el micrófono de tus auriculares en Configuración > Sistema > Sonido > Entrada.");
            return;
        }

        _partialText = string.Empty;
        _userVoicePrediction = text.ToLowerInvariant();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            VoiceResultLabel.Text = $"✅ Dijiste: \"{text}\"";
            AnalyzeButton.IsVisible = true;
            StatusLabel.Text = "Voz reconocida correctamente";
        });
    }
#endif

    private async Task StopListeningAsync()
    {
        _isListening = false;
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            MicrophoneButton.ImageSource = "microfonoestudio.png";
            MicrophoneButton.Text = "Usar Micrófono";
        });

        try
        {
            _cts?.Cancel();
            
            // Stop listening and release the platform recognizer.
#if WINDOWS
            if (_windowsSpeechRecognizer is not null)
            {
                var recognizer = _windowsSpeechRecognizer;
                _windowsSpeechRecognizer = null;
                await recognizer.StopRecognitionAsync();
                recognizer.HypothesisGenerated -= OnWindowsHypothesisGenerated;
                recognizer.RecognitionQualityDegrading -= OnWindowsRecognitionQualityDegrading;
            }
#else
            await _speechToText.StopListenAsync(CancellationToken.None);
#endif
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = "Escucha parada";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error parando reconocimiento: {ex.Message}");
        }
        finally
        {
            CleanupEventHandlers();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CleanupEventHandlers()
    {
#if !WINDOWS
        _speechToText.RecognitionResultUpdated -= OnPartialResult;
        _speechToText.RecognitionResultCompleted -= OnFinalResult;
#endif
    }

    private void OnPartialResult(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
    {
        try
        {
            var texto = e.RecognitionResult?.Trim() ?? "";
            if (string.IsNullOrEmpty(texto))
                return;

            _partialText = texto;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                VoiceResultLabel.Text = $"Escuchando: {texto}";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en resultado parcial: {ex.Message}");
        }
    }

    private void OnFinalResult(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e)
    {
        try
        {
            var recognitionResult = e.RecognitionResult;
            if (!recognitionResult.IsSuccessful)
            {
                var error = recognitionResult.Exception?.GetBaseException().Message;
                var message = string.IsNullOrWhiteSpace(error)
                    ? "Windows no pudo completar el reconocimiento."
                    : $"Windows no pudo completar el reconocimiento: {error}";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    VoiceResultLabel.Text = $"❌ {message}\nComprueba la conexión a Internet y activa Reconocimiento de voz en línea en Configuración > Privacidad y seguridad > Voz.";
                    AnalyzeButton.IsVisible = false;
                });
                Debug.WriteLine($"Error de reconocimiento de voz: {message}");
                return;
            }

            _finalText = recognitionResult.Text?.Trim().ToLowerInvariant() ?? "";

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (string.IsNullOrEmpty(_finalText))
                {
                    VoiceResultLabel.Text = "❌ No se recibió ninguna palabra. Habla en español durante unos segundos y comprueba que Windows tenga habilitado Reconocimiento de voz en línea en Configuración > Privacidad y seguridad > Voz.";
                    AnalyzeButton.IsVisible = false;
                }
                else
                {
                    VoiceResultLabel.Text = $"✅ Dijiste: \"{_finalText}\"";
                    AnalyzeButton.IsVisible = true;
                    StatusLabel.Text = "Voz reconocida correctamente";
                }
            });

            // Guardar predicción del usuario para comparar con Azure
            _userVoicePrediction = _finalText;

            _partialText = "";
            _finalText = "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en resultado final: {ex.Message}");
        }
    }

    private async void OnAnalyzeButtonClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath) || !File.Exists(_currentImagePath))
        {
            await DisplayAlert("Error", "No hay imagen para analizar", "OK");
            return;
        }

        try
        {
            AnalyzeButton.IsEnabled = false;
            StatusLabel.Text = "Analizando imagen con Azure AI...";

            var predictions = await _customVisionService.GetAllPredictionsAsync(_currentImagePath);

            if (predictions?.Count > 0)
            {
                var topPrediction = predictions.First();

                _currentAnalysis = new AnalysisResult
                {
                    UserPrediction = _userVoicePrediction,
                    AIPrediction = topPrediction.TagName,
                    Confidence = topPrediction.Probability,
                    AnalysisTime = DateTime.Now,
                    AllPredictions = predictions
                };

                UpdateAnalysisResults();
                StatusLabel.Text = "Análisis completado";
            }
            else
            {
                await DisplayAlert("Error", "No se pudo obtener predicción de Azure", "OK");
                StatusLabel.Text = "Error en análisis";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en análisis Azure: {ex.Message}");
            Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            await DisplayAlert("Error de conexión",
                $"No se pudo conectar con Azure Custom Vision. Revisa appsettings.local.json. Detalle: {ex.Message}", "OK");

            StatusLabel.Text = "Error en conexión con Azure";
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
            ClearButton.IsVisible = true;
        }
    }

    private void OnClearButtonClicked(object sender, EventArgs e)
    {
        ResetEverything();
    }

    private void UpdateAnalysisResults()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UserPredictionLabel.Text = string.IsNullOrEmpty(_currentAnalysis.UserPrediction)
                ? "-"
                : _currentAnalysis.UserPrediction;

            AzurePredictionLabel.Text = _currentAnalysis.AIPrediction;
            ConfidenceLabel.Text = $"{_currentAnalysis.Confidence:P2}";
            FinalResultLabel.Text = _currentAnalysis.ResultMessage;

            if (_currentAnalysis.IsMatch)
            {
                FinalResultLabel.TextColor = Color.FromArgb("#4CAF50"); // Verde
            }
            else
            {
                FinalResultLabel.TextColor = Color.FromArgb("#F44336"); // Rojo
            }

            AzureResultFrame.IsVisible = true;
        });
    }

    private void ResetEverything()
    {
        // Stop listening if active
        if (_isListening)
        {
            _ = StopListeningAsync();
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SelectedImage.Source = "placeholder.png";
            NoImageLabel.IsVisible = true;
            _currentImagePath = string.Empty;

            // Limpiar gestos
            ResetImageTransformations();

            // Limpiar predicciones
            _userVoicePrediction = string.Empty;
            _currentAnalysis = new AnalysisResult();

            // Ocultar frames y botones
            VoiceResultFrame.IsVisible = false;
            AzureResultFrame.IsVisible = false;
            AnalyzeButton.IsVisible = false;
            ClearButton.IsVisible = false;

            // Resetear labels
            VoiceResultLabel.Text = "Esperando reconocimiento...";
            UserPredictionLabel.Text = "-";
            AzurePredictionLabel.Text = "-";
            ConfidenceLabel.Text = "-";
            FinalResultLabel.Text = "";

            // Actualizar estado
            StatusLabel.Text = "Selecciona una imagen para comenzar";

            Debug.WriteLine("Todo limpiado - Listo para empezar de nuevo");
        });
    }

    // Método para cargar imagen
    private async Task LoadImageFromFile(string filePath)
    {
        try
        {
            _currentImagePath = filePath;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SelectedImage.Source = ImageSource.FromFile(filePath);
                NoImageLabel.IsVisible = false;
                VoiceResultFrame.IsVisible = false;
            });

            Debug.WriteLine($"Imagen cargada: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al cargar imagen: {ex.Message}");
            await DisplayAlert("Error", "No se pudo cargar la imagen", "OK");
        }
    }

    // Doble toque: alternar zoom 2x
    private async void OnDoubleTapTapped(object sender, EventArgs e)
    {
        if (SelectedImage.Source == null || SelectedImage.Source is FileImageSource { File: "placeholder.png" })
            return;

        if (_currentScale < ZOOM_LEVEL_2X)
        {
            await ZoomToLevel(ZOOM_LEVEL_2X);
            StatusLabel.Text = "Zoom 2x aplicado";
        }
        else
        {
            await ResetImageTransformations();
            StatusLabel.Text = "Imagen restablecida";
        }
    }

    // Pan (arrastre): solo funciona con zoom activo
    private void OnPanGestureUpdated(object sender, PanUpdatedEventArgs e)
    {
        // Solo permitir pan si hay zoom (> 1.0)
        if (_currentScale <= 1.0)
        {
            SelectedImage.TranslationX = 0;
            SelectedImage.TranslationY = 0;
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _startX = SelectedImage.TranslationX;
                _startY = SelectedImage.TranslationY;
                break;

            case GestureStatus.Running:
                double newX = _startX + e.TotalX;
                double newY = _startY + e.TotalY;

                double maxMovement = 100 * (_currentScale - 1);
                newX = Math.Max(-maxMovement, Math.Min(maxMovement, newX));
                newY = Math.Max(-maxMovement, Math.Min(maxMovement, newY));

                SelectedImage.TranslationX = newX;
                SelectedImage.TranslationY = newY;
                break;

            case GestureStatus.Completed:
                _lastX = SelectedImage.TranslationX;
                _lastY = SelectedImage.TranslationY;
                break;
        }
    }

    // Método para aplicar zoom con animación
    private async Task ZoomToLevel(double targetScale)
    {
        targetScale = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, targetScale));

        await SelectedImage.ScaleTo(targetScale, 300, Easing.CubicInOut);
        _currentScale = targetScale;

        UpdateZoomIndicator();
    }

    // Método para resetear transformaciones
    private async Task ResetImageTransformations()
    {
        await Task.WhenAll(
            SelectedImage.ScaleTo(1.0, 300, Easing.CubicInOut),
            SelectedImage.TranslateTo(0, 0, 300, Easing.CubicInOut)
        );

        _currentScale = 1.0;
        SelectedImage.TranslationX = 0;
        SelectedImage.TranslationY = 0;
        _lastX = 0;
        _lastY = 0;

        UpdateZoomIndicator();
    }

    // Actualizar indicador de zoom
    private void UpdateZoomIndicator()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_currentScale > 1.0)
            {
                ZoomLabel.Text = $"Zoom: {_currentScale:F1}x";
                ZoomLabel.IsVisible = true;
            }
            else
            {
                ZoomLabel.IsVisible = false;
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isListening)
        {
            _ = StopListeningAsync();
        }
    }
}
