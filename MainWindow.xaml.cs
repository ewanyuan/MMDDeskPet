using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace MMDDeskPet;

public partial class MainWindow : Window
{
    private const double SinglePetWidth = 480.0;
    private const double DualPetWidth = 1040.0;
    private const string AppVirtualHost = RuntimeAssetUrls.AppHost;
    private static readonly string[] SupportedMusicExtensions =
    [
        ".mp3",
        ".wav",
        ".ogg",
        ".m4a",
        ".aac",
        ".flac",
        ".mp4",
    ];
    private readonly bool _autoLoad;
    private readonly string _repositoryRoot;
    private readonly string[] _modelPaths;
    private readonly DanceMotionDefinition[] _danceMotions;
    private readonly bool _english;
    private readonly UserSettings _settings;
    private readonly ContextMenu _petMenu = new();
    private readonly MenuItem _openModelMenu = new();
    private readonly MenuItem _openMotionMenu = new();
    private readonly MenuItem _openMusicMenu = new();
    private readonly MenuItem _recentModelsMenu = new();
    private readonly MenuItem _recentMotionsMenu = new();
    private readonly MenuItem _recentMusicMenu = new();
    private readonly MenuItem _libraryModelsMenu = new();
    private readonly MenuItem _libraryMotionsMenu = new();
    private readonly MenuItem _libraryMusicMenu = new();
    private readonly MenuItem _openLibraryFolderMenu = new();
    private readonly MenuItem _rescanLibraryMenu = new();
    private readonly List<FileSystemWatcher> _libraryWatchers = [];
    private DispatcherTimer? _libraryRefreshTimer;
    private ModelLibrary _library = ModelLibrary.Empty;
    private readonly MenuItem _switchCharacterMenu = new();
    private readonly MenuItem _danceMenu = new();
    private readonly MenuItem _builtInDanceMenu = new();
    private readonly MenuItem _customMotionMenu = new();
    private readonly MenuItem _noDanceMenu = new();
    private readonly MenuItem _adultContentToggle = new();
    private readonly MenuItem _pauseDanceMenu = new();
    private readonly MenuItem _loopMotionMenu = new();
    private readonly MenuItem _appearanceMenu = new();
    private readonly MenuItem _skinToneMenu = new();
    private readonly MenuItem _nakedMenu = new();
    private readonly MenuItem _eyeExpressionMenu = new();
    private readonly MenuItem _mouthExpressionMenu = new();
    private readonly MenuItem _clothingMenu = new();
    private readonly MenuItem _adultMenu = new();
    private readonly MenuItem _allMorphsMenu = new();
    private readonly MenuItem _resetMorphsMenu = new();
    private readonly MenuItem _alwaysOnTopMenu = new();
    private readonly MenuItem _closeMenu = new();
    private readonly TaskCompletionSource<bool> _runtimeReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<int, MenuItem> _morphMenuItems = [];
    private readonly Dictionary<MenuItem, int> _activeMorphBySubmenu = [];
    private readonly List<MenuItem> _danceMenuItems = [];
    private readonly Dictionary<string, MenuItem> _skinToneMenuItems = [];
    private static readonly SolidColorBrush MenuHoverBackground = CreateBrush(0xF4, 0xDC, 0xEB);
    private static readonly SolidColorBrush MenuHoverForeground = CreateBrush(0x7D, 0x31, 0x5E);
    private static readonly IReadOnlyDictionary<string, string> ChineseMorphNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Blink"] = "眨眼",
            ["Blink_L"] = "左眨眼",
            ["Blink_R"] = "右眨眼",
            ["Laugh"] = "笑",
            ["Laugh_L"] = "左笑眼",
            ["Laugh_R"] = "右笑眼",
            ["Fleer"] = "嘲笑",
            ["Relax"] = "放松",
            ["Jitome"] = "斜眼",
            ["EyeAngry"] = "生气眼",
            ["Surprised"] = "惊讶",
            ["SadSquint"] = "悲伤眼",
            ["Squint"] = "眯眼",
            ["SquintLow"] = "低眼",
            ["Puzzled"] = "疑惑",
            ["UpperLid"] = "上眼睑",
            ["LowerLid"] = "下眼睑",
            ["LowerSneer"] = "轻蔑",
            ["LookUP"] = "向上看",
            ["LookDOWN"] = "向下看",
            ["LookNEAR"] = "斗鸡眼",
            ["LookAT"] = "看向前方",
            ["Iris-"] = "缩小虹膜",
            ["Pupil-"] = "缩小瞳孔",
            ["BlackEye"] = "黑眼",
            ["PupilHeart"] = "爱心眼",
            ["StarEyes"] = "星星眼",
            ["x_HiLit"] = "隐藏眼睛高光",
            ["x_Pupil"] = "隐藏瞳孔",
            ["Serious"] = "认真",
            ["Anger"] = "生气",
            ["BrowMid+"] = "眉心上提",
            ["BrowMid-"] = "眉心下压",
            ["BrowIn+"] = "眉头上提",
            ["BrowIn-"] = "眉头下压",
            ["BrowInTip+"] = "内眉上提",
            ["BrowInTip-"] = "内眉下压",
            ["BrowOut+"] = "眉尾上提",
            ["BrowOut-"] = "眉尾下压",
            ["BrowV+"] = "眉毛上提",
            ["BrowV-"] = "眉毛下压",
            ["BrowZFix"] = "眉毛防遮挡",
            ["Aa"] = "啊",
            ["Ih"] = "咿",
            ["Ou"] = "呜",
            ["Ee"] = "诶",
            ["Oh"] = "哦",
            ["Whee"] = "欢呼",
            ["Shout"] = "大喊",
            ["Nya"] = "喵",
            ["Sniff"] = "嗅闻",
            ["LipMid+"] = "嘴唇上提",
            ["LipMid-"] = "嘴唇下压",
            ["CornerUP"] = "嘴角上扬",
            ["CornerDOWN"] = "嘴角下压",
            ["CornerWIDE"] = "嘴角扩大",
            ["TongueSmile_L"] = "左侧吐舌",
            ["TongueSmile_R"] = "右侧吐舌",
            ["FoolSmile"] = "流口水",
            ["MouthUP"] = "嘴巴上移",
            ["MouthDOWN"] = "嘴巴下移",
            ["MouthLEFT"] = "嘴巴左移",
            ["MouthRIGHT"] = "嘴巴右移",
            ["JawFront"] = "下巴前移",
            ["x_Teeth"] = "隐藏牙齿",
            ["TeethOpen"] = "张牙",
            ["TeethClose"] = "收牙",
            ["TeethBack"] = "牙齿后移",
            ["TeethWIDE"] = "牙齿扩大",
            ["TeethGiza"] = "锯齿",
            ["TongueSharpe+"] = "舌头展开",
            ["TongueSharpe-"] = "舌头收拢",
            ["ShadowShade"] = "阴影",
            ["BlushLine"] = "害羞线",
            ["Flattered"] = "害羞",
            ["FaceShade"] = "脸部阴影",
            ["Sweat"] = "汗",
            ["Tattoo"] = "纹身",
            ["x_Barefoot"] = "赤脚",
            ["x_OnlyShoes"] = "仅保留鞋子",
            ["x_OnlyStocking"] = "仅保留长袜",
            ["x_Choker"] = "项圈",
            ["x_Skirt"] = "裙子",
            ["x_TankTop"] = "背心",
            ["TTop_Slip"] = "胸口衣物下滑",
            ["x_Pants"] = "短裤",
            ["TbTop_SlipUP"] = "胸口衣物上移",
            ["TbTop_SlipDOWN"] = "胸口衣物下移",
            ["x_Tubetop"] = "抹胸",
            ["x_T-Back"] = "丁字裤",
            ["Blowjob"] = "口部形变",
            ["NippleSize+"] = "乳头变大",
            ["NippleStand"] = "乳头挺立",
            ["NippleSize-"] = "乳头变小",
            ["Boko"] = "腹部受击",
            ["BokoInv"] = "腹部凹陷",
            ["BokoLine"] = "腹部线条",
            ["CliSize+"] = "阴蒂变大",
            ["LabOpen_S"] = "阴唇打开（小）",
            ["LabOpen_M"] = "阴唇打开（中）",
            ["LabOpen_L"] = "阴唇打开（大）",
            ["LabOpen_XL"] = "阴唇打开（特大）",
            ["AnaOpen"] = "肛部打开",
            ["AnaOpenEX"] = "肛部打开（异物）",
        };
    private readonly object _requestLock = new();
    private readonly RuntimeRequestGate _requests = new();
    private readonly List<EventWaiter> _eventWaiters = [];
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly Queue<string> _commandsBeforeReady = new();
    private Task? _initializeTask;
    private int _startupStarted;
    private bool _sawPetNavigation;
    private string? _lastRuntimeError;
    private int _currentCharacter;
    private bool _browserInitialized;
    private bool _navigationStarted;
    private bool _closing;
    private bool _dualMode;
    private bool _fullNudeActive;
    private bool _adultContentEnabled;
    private bool _modelLoaded;
    private bool _usingCustomModel;
    private bool _modelHasNakedMorph;
    private bool _modelHasAdultMorphs;
    private bool _modelHasClothingMorphs;
    private string? _activeModelPath;
    private string? _customMotionPath;
    private bool _userMotionActive;
    private readonly ExternalFileMap _externalFiles = new();
    private readonly LaunchOptions _launch;
    internal bool SuppressDialogs { get; set; }
    private MenuItem? _activeDanceMenuItem;
    private MenuItem? _hoveredMenuItem;
    private string TracePath => Path.Combine(Directory.GetCurrentDirectory(), "mmd-runtime-self-test-trace.txt");

    internal MainWindow(bool autoLoad, LaunchOptions? launch = null)
    {
        _autoLoad = autoLoad;
        _launch = launch ?? new LaunchOptions();
        _repositoryRoot = FindRepositoryRoot();
        _modelPaths =
        [
            ResolveModelPath(
                "Hime_260426",
                "Hime.physics-stable.pmx"),
            ResolveModelPath(
                "Mikoto_260303",
                "Mikoto.physics-stable.pmx"),
        ];
        _danceMotions =
        [
            new(
                "snow-halation",
                "大好きなSnow halation",
                "Daisuki na Snow halation",
                Path.Combine(
                    "assets",
                    "motions",
                    "snow-halation-natsuki",
                    "nac_snow_halation",
                    "nac_snow_halation.vmd"),
                UsageNote: "用户指定来源：ss46474765；沿用本机已核验的 Snow halation VMD。"),
            new(
                "sparkle-dual",
                "アニサマ2022「Sparkle」",
                "Animelo Summer Live 2022 \"Sparkle\"",
                Path.Combine(
                    "assets",
                    "motions",
                    "sparkle-dual",
                    "sparkle-left.vmd"),
                Path.Combine(
                    "assets",
                    "motions",
                    "sparkle-dual",
                    "sparkle-right.vmd"),
                UsageNote: "用户指定来源：ss46421758；BowlRoll 284475，动作作者：松山じろべえ。左位模型使用左位动作，右位模型使用右位动作；允许修改，但禁止未经许可再分发和商业使用。"),
            new(
                "helltaker",
                "Helltaker（循环舞）",
                "Helltaker (loop dance)",
                Path.Combine(
                    "assets",
                    "motions",
                    "helltaker",
                    "Helltaker_like_dance_1min_1.vmd")),
            new(
                "telepathy",
                "Telepathy（上半身）",
                "Telepathy (upper body)",
                Path.Combine(
                    "assets",
                    "motions",
                    "telepathy",
                    "P_Telepathy_motion_",
                    "Telepathy1235.vmd")),
            new(
                "beyond-the-way",
                "Beyond the way（力量感）",
                "Beyond the way (energetic)",
                Path.Combine(
                    "assets",
                    "motions",
                    "beyond-the-way",
                    "P_BeyondTW_Motion",
                    "BeyoudTW_motion.vmd")),
            new(
                "tokyo-shandy-rendezvous",
                "东京・香迪・兰德弗（风格化）",
                "Tokyo Shandy Rendezvous (stylized)",
                Path.Combine(
                    "assets",
                    "motions",
                    "tokyo-shandy-rendezvous",
                    "P_TokyoSR_モーション配布",
                    "TokyoSR_motion.vmd")),
            new(
                "queen",
                "QUEEN（酷帅）",
                "QUEEN (sharp)",
                Path.Combine(
                    "assets",
                    "motions",
                    "queen",
                    "QUEEN_miku.vmd")),
            new(
                "womanizer-dual",
                "Womanizer（双人・魅惑）",
                "Womanizer (dual, sensual)",
                Path.Combine(
                    "assets",
                    "motions",
                    "womanizer",
                    "womanizer-left.vmd"),
                Path.Combine(
                    "assets",
                    "motions",
                    "womanizer",
                    "womanizer-right.vmd")),
            new(
                "erotic-hip-duo",
                "Erotic Hip-Shaking Duo（双人）",
                "Erotic Hip-Shaking Duo (dual)",
                Path.Combine(
                    "assets",
                    "motions",
                    "erotic-hip-duo",
                    "EroHipDuoDance_Model-L_Motion.vmd"),
                Path.Combine(
                    "assets",
                    "motions",
                    "erotic-hip-duo",
                    "EroHipDuoDance_Model-R_Motion.vmd"),
                UsageNote: "动作作者 CraftieMMD；允许修改和 R18 使用，但禁止未经许可再分发动作数据。本机版本先只加载双人身体动作，不自动加载面部、镜头或持杆附加动作。",
                StartFrame: 330),
            new(
                "waist-dance",
                "腰振り舞（诱惑）",
                "Waist dance (sensual)",
                Path.Combine(
                    "assets",
                    "motions",
                    "waist-dance",
                    "waist-dance-loop.vmd"),
                Loop: true),
            new(
                "rust-veins",
                "Rust Veins 腰振舞",
                "Rust Veins hip-sway dance",
                Path.Combine(
                    "assets",
                    "motions",
                    "rust-veins",
                    "motion 1.vmd"),
                UsageNote: "动作作者 TottyMMD（totozoMMD）；仅作本机使用，使用前请保留随包 readme.txt 并自行确认发布范围。"),
            new(
                "tsuyoi",
                "つよっ！（单人）",
                "Tsuyoi! (solo)",
                Path.Combine(
                    "assets",
                    "motions",
                    "requested",
                    "tsuyoi",
                    "tsuyoi.vmd"),
                UsageNote: "非商业使用；禁止 R18、再分发或转交动作文件。"),
            new(
                "inmu-king",
                "INMU KING（动作待补）",
                "INMU KING (motion pending)",
                Path.Combine(
                    "assets",
                    "motions",
                    "requested",
                    "inmu-king",
                    "INMU-KING.vmd"),
                UnavailableNote: "原视频未公开可核验的 VMD 动作文件，暂不伪造动作。"),
            new(
                "tick-trick",
                "Tick-Trick（Rick式）",
                "Tick-Trick (Rick)",
                Path.Combine(
                    "assets",
                    "motions",
                    "requested",
                    "tick-trick",
                    "Tick-Trick.vmd"),
                UsageNote: "非商业、非 R15；禁止再分发，且需保留乐曲作者前線的署名。"),
            new(
                "oppai-fukkireta",
                "Oppai Fukkireta（授权待定）",
                "Oppai Fukkireta (license pending)",
                Path.Combine(
                    "assets",
                    "motions",
                    "requested",
                    "oppai-fukkireta",
                    "Oppai-Fukkireta.vmd"),
                UnavailableNote: "原作者条款禁止将动作嵌入游戏，当前不能合法集成。"),
        ];
        _english = !IsChineseSystemLanguage();
        _settings = UserSettingsStore.Load();
        _adultContentEnabled = _settings.AdultContentEnabled;
        try
        {
            ModelLibraryScanner.EnsureFolders(CurrentLibraryRoot());
        }
        catch (Exception exception)
        {
            Trace($"library-folders:{exception.GetBaseException().Message}");
        }

        InitializeComponent();
        Title = "MMD Desk Pet";
        Topmost = true;
        Browser.AllowExternalDrop = false;
        BuildBaseContextMenu();
        StartLibraryWatch();
        Browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 0, 0, 0);
        Loaded += OnLoaded;
    }

    public string RepositoryRoot => _repositoryRoot;

    internal IReadOnlyList<DanceMotionDefinition> DanceMotions => _danceMotions;

    internal string GetDanceMotionPath(DanceMotionDefinition dance)
    {
        return ResolveProjectPath(dance.RelativePath);
    }

    internal string? GetSecondaryDanceMotionPath(DanceMotionDefinition dance)
    {
        return dance.SecondaryRelativePath is null
            ? null
            : ResolveProjectPath(dance.SecondaryRelativePath);
    }

    internal string? GetDanceMusicPath(DanceMotionDefinition dance)
    {
        var musicDirectory = ResolveProjectPath(
            Path.Combine("assets", "music"));
        if (Directory.Exists(musicDirectory))
        {
            var musicPath = Directory.EnumerateFiles(musicDirectory)
                .FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        dance.Key,
                        StringComparison.OrdinalIgnoreCase)
                    && SupportedMusicExtensions.Contains(
                        Path.GetExtension(path),
                        StringComparer.OrdinalIgnoreCase));
            if (musicPath is not null)
            {
                return musicPath;
            }
        }

        return null;
    }

    internal async Task<JsonElement> LoadDanceMusicAsync(DanceMotionDefinition dance)
    {
        var musicPath = GetDanceMusicPath(dance);
        if (musicPath is null)
        {
            return await SendRuntimeRequestAsync(
                "clear-music",
                "music-cleared",
                requestId => new { type = "clear-music", requestId },
                supersedePreviousLoad: false);
        }

        var url = ToRuntimeUrl(musicPath);
        return await SendRuntimeRequestAsync(
            "load-music",
            "music-loaded",
            requestId => new { type = "load-music", url, requestId },
            supersedePreviousLoad: false);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_autoLoad)
        {
            Trace("startup-skipped:auto-load-disabled");
            return;
        }

        if (Interlocked.Exchange(ref _startupStarted, 1) != 0)
        {
            Trace("startup-skipped:already-started");
            return;
        }

        Trace("startup:interactive");
        try
        {
            await InitializeRuntimeAsync();
            var loaded = false;
            if (IsExistingFile(_launch.ModelPath))
            {
                Trace("startup-plan:command-line-model");
                await LoadModelFileAsync(_launch.ModelPath!, recordRecent: true);
                loaded = true;
                if (!string.IsNullOrWhiteSpace(_launch.MotionPath))
                {
                    if (!IsExistingFile(_launch.MotionPath))
                    {
                        throw new FileNotFoundException("The VMD motion was not found.", _launch.MotionPath);
                    }

                    await LoadMotionFileAsync(_launch.MotionPath, recordRecent: true);
                }
            }
            else if (!string.IsNullOrWhiteSpace(_launch.ModelPath))
            {
                throw new FileNotFoundException("The PMX model was not found.", _launch.ModelPath);
            }
            else
            {
                if (StartupModelPolicy.ShouldRestoreLastModel(_settings.LastModelPath, IsExistingFile))
                {
                    Trace("startup-plan:restore-last-model");
                    try
                    {
                        await LoadModelFileAsync(_settings.LastModelPath!, recordRecent: false);
                        loaded = true;
                        if (IsExistingFile(_settings.LastMotionPath))
                        {
                            try
                            {
                                await LoadMotionFileAsync(_settings.LastMotionPath!, recordRecent: false);
                            }
                            catch (Exception motionError)
                            {
                                Trace($"startup-motion-skipped:{motionError.Message}");
                            }
                        }
                    }
                    catch (Exception modelError) when (IsSuperseded(modelError))
                    {
                        throw;
                    }
                    catch (Exception modelError)
                    {
                        Trace($"startup-model-skipped:{modelError.GetBaseException().Message}");
                    }
                }

                if (!loaded)
                {
                    loaded = await TryLoadFirstLibraryModelAsync();
                }

                if (!loaded)
                {
                    Trace("startup-plan:empty");
                }
            }

            if (!StartupModelPolicy.ShouldAutoLoadBundledModel && !loaded)
            {
                ShowEmptyHint();
            }
            else if (loaded && _currentCharacter == 0 && !_usingCustomModel)
            {
                _ = PreloadSecondaryCharacterWhenIdleAsync();
            }

            UpdateSwitchCharacterMenu();
            ActivatePetFocus();
        }
        catch (Exception exception) when (IsSuperseded(exception))
        {
            Trace($"startup-superseded:{exception.GetBaseException().Message}");
            if (!_modelLoaded)
            {
                ShowEmptyHint();
            }
        }
        catch (TimeoutException exception)
        {
            Trace($"startup-timeout:{exception.Message}");
            if (!_modelLoaded)
            {
                ShowEmptyHint();
            }
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
            Trace($"startup-failure:{exception}");
            ShowStartupError(exception);
            Close();
        }
    }

    private async Task PreloadSecondaryCharacterWhenIdleAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2.5));
            if (_closing
                || _dualMode
                || _currentCharacter != 0
                || !_switchCharacterMenu.IsEnabled
                || !_danceMenu.IsEnabled)
            {
                return;
            }

            BeginSecondaryCharacterPreload(0, 1);
        }
        catch
        {
            // Idle preloading is an optimization and must never affect startup.
        }
    }

    private void ShowStartupError(Exception exception)
    {
        if (SuppressDialogs)
        {
            Trace($"startup-dialog-suppressed:{exception.GetBaseException().Message}");
            return;
        }

        var rootException = exception.GetBaseException();
        var message = rootException.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
            ? Text(
                "桌宠需要 Microsoft Edge WebView2 Runtime。请先安装它，再重新双击程序。",
                "This desktop pet needs the Microsoft Edge WebView2 Runtime. Install it, then start the app again.")
            : Text(
                "桌宠启动失败。请确认你运行的是完整文件夹里的 exe，而不是只复制了 exe 文件。\n\n"
                    + rootException.Message,
                "The desktop pet could not start. Make sure you are running the exe from the complete folder, not a copied exe file.\n\n"
                    + rootException.Message);

        MessageBox.Show(
            this,
            message,
            "MMD Desk Pet",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public Task InitializeRuntimeAsync()
    {
        lock (_requestLock)
        {
            return _initializeTask ??= InitializeRuntimeCoreAsync();
        }
    }

    private async Task InitializeRuntimeCoreAsync()
    {
        Trace("initialize-start");
        if (!_browserInitialized)
        {
            var dataDirectory = Path.Combine(
                Path.GetTempPath(),
                "MMDDeskPet-WebView2-" + Guid.NewGuid().ToString("N"));
            Trace("before-environment");
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDirectory);
            Trace("after-environment");
            Browser.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
            Trace("before-ensure");
            await Browser.EnsureCoreWebView2Async(environment);
            Trace("after-ensure");
            _browserInitialized = true;
        }

        if (!_navigationStarted)
        {
            var core = Browser.CoreWebView2
                ?? throw new InvalidOperationException("WebView2 did not initialize.");
            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += OnNavigationCompleted;
            core.ProcessFailed += OnProcessFailed;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            // Folder mapping answers pet.local itself and never raises
            // WebResourceRequested, which is why /ext/ requests died with status 0
            // and no handler log. Every response is streamed by the handler below.
            Trace($"virtual-host-mapping:none:{_repositoryRoot}");
            var resourceFilter = $"https://{AppVirtualHost}/*";
            var sourceKinds = false;
            try
            {
                core.AddWebResourceRequestedFilter(
                    resourceFilter,
                    CoreWebView2WebResourceContext.All,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                sourceKinds = true;
            }
            catch (MissingMethodException exception)
            {
                Trace($"resource-filter-fallback:{exception.GetType().Name}:{exception.Message}");
            }
            catch (EntryPointNotFoundException exception)
            {
                Trace($"resource-filter-fallback:{exception.GetType().Name}:{exception.Message}");
            }

            if (!sourceKinds)
            {
                core.AddWebResourceRequestedFilter(resourceFilter, CoreWebView2WebResourceContext.All);
            }

            core.WebResourceRequested += OnPetHostWebResourceRequested;
            Trace($"resource-filter:{resourceFilter}:source-kinds={(sourceKinds ? "all" : "legacy")}");
            Browser.AllowExternalDrop = false;
            _navigationStarted = true;
            Trace("before-navigate");
            var webIndexPath = ResolveProjectPath(
                Path.Combine("web", "index.html"));
            core.Navigate(ToRuntimeUrl(webIndexPath));
            Trace("after-navigate");
        }

        Trace("before-ready");
        await _runtimeReady.Task.WaitAsync(TimeSpan.FromSeconds(45));
        Trace("after-ready");
    }

    public async Task<JsonElement> LoadCharacterAsync(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= _modelPaths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(characterIndex));
        }

        return await LoadModelFileAsync(
            _modelPaths[characterIndex],
            characterIndex,
            recordRecent: false);
    }

    public async Task<JsonElement> LoadSecondaryCharacterAsync(int primaryIndex, int secondaryIndex)
    {
        if (primaryIndex < 0 || primaryIndex >= _modelPaths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(primaryIndex));
        }
        if (secondaryIndex < 0 || secondaryIndex >= _modelPaths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(secondaryIndex));
        }
        if (_dualMode || _currentCharacter != primaryIndex)
        {
            throw new InvalidOperationException("The incremental dual-character path requires the requested primary character to be active.");
        }

        var secondaryPath = _modelPaths[secondaryIndex];
        if (!File.Exists(secondaryPath))
        {
            throw new FileNotFoundException("The secondary PMX model was not found.", secondaryPath);
        }

        var secondaryUrl = ToRuntimeUrl(secondaryPath);
        var message = await SendRuntimeRequestAsync(
            "load-secondary-character",
            "dual-models-loaded",
            requestId => new
            {
                type = "load-secondary-character",
                primaryIndex,
                secondaryIndex,
                secondaryUrl,
                requestId,
            },
            supersedePreviousLoad: true);
        PopulateMorphMenus(message.GetProperty("primary"));
        MarkDualModelsLoaded(_modelPaths[primaryIndex], primaryIndex);
        await ApplyFullNudeIfNeededAsync();
        return message;
    }

    public async Task<JsonElement> LoadMotionAsync(string name, string path, bool loop = false)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The VMD motion was not found.", path);
        }

        var url = ToRuntimeUrl(path);
        return await SendRuntimeRequestAsync(
            "load-motion",
            "motion-loaded",
            requestId => new
            {
                type = "load-motion",
                name,
                url,
                loop,
                requestId,
            },
            supersedePreviousLoad: true);
    }

    public async Task<JsonElement> LoadDualCharactersAsync(int primaryIndex, int secondaryIndex)
    {
        if (primaryIndex < 0 || primaryIndex >= _modelPaths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(primaryIndex));
        }
        if (secondaryIndex < 0 || secondaryIndex >= _modelPaths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(secondaryIndex));
        }

        var primaryPath = _modelPaths[primaryIndex];
        var secondaryPath = _modelPaths[secondaryIndex];
        if (!File.Exists(primaryPath))
        {
            throw new FileNotFoundException("The primary PMX model was not found.", primaryPath);
        }
        if (!File.Exists(secondaryPath))
        {
            throw new FileNotFoundException("The secondary PMX model was not found.", secondaryPath);
        }

        var primaryUrl = ToRuntimeUrl(primaryPath);
        var secondaryUrl = ToRuntimeUrl(secondaryPath);
        var message = await SendRuntimeRequestAsync(
            "load-dual-character",
            "dual-models-loaded",
            requestId => new
            {
                type = "load-dual-character",
                primaryIndex,
                primaryUrl,
                secondaryIndex,
                secondaryUrl,
                requestId,
            },
            supersedePreviousLoad: true);
        PopulateMorphMenus(message.GetProperty("primary"));
        MarkDualModelsLoaded(primaryPath, primaryIndex);
        await ApplyFullNudeIfNeededAsync();
        return message;
    }

    public async Task<JsonElement> LoadDualMotionAsync(
        string name,
        string primaryPath,
        string secondaryPath,
        int startFrame = 0)
    {
        if (!File.Exists(primaryPath))
        {
            throw new FileNotFoundException("The primary VMD motion was not found.", primaryPath);
        }
        if (!File.Exists(secondaryPath))
        {
            throw new FileNotFoundException("The secondary VMD motion was not found.", secondaryPath);
        }

        var primaryUrl = ToRuntimeUrl(primaryPath);
        var secondaryUrl = ToRuntimeUrl(secondaryPath);
        return await SendRuntimeRequestAsync(
            "load-dual-motion",
            "dual-motion-loaded",
            requestId => new
            {
                type = "load-dual-motion",
                name,
                primaryUrl,
                secondaryUrl,
                startFrame,
                requestId,
            },
            supersedePreviousLoad: true);
    }

    internal void BeginDualMotionPreload(DanceMotionDefinition dance)
    {
        if (!dance.IsDual)
        {
            return;
        }

        var secondaryPath = GetSecondaryDanceMotionPath(dance)
            ?? throw new InvalidDataException("The dual dance has no secondary VMD path.");
        var primaryPath = GetDanceMotionPath(dance);
        if (!File.Exists(primaryPath) || !File.Exists(secondaryPath))
        {
            return;
        }

        PostCommand(new
        {
            type = "preload-dual-motion",
            name = dance.Key,
            primaryUrl = ToRuntimeUrl(primaryPath),
            secondaryUrl = ToRuntimeUrl(secondaryPath),
        });
    }

    internal void BeginSecondaryCharacterPreload(int primaryIndex, int secondaryIndex)
    {
        if (primaryIndex < 0 || primaryIndex >= _modelPaths.Length
            || secondaryIndex < 0 || secondaryIndex >= _modelPaths.Length
            || _dualMode
            || _currentCharacter != primaryIndex)
        {
            return;
        }

        var secondaryPath = _modelPaths[secondaryIndex];
        if (!File.Exists(secondaryPath))
        {
            return;
        }

        PostCommand(new
        {
            type = "preload-secondary-character",
            primaryIndex,
            secondaryIndex,
            secondaryUrl = ToRuntimeUrl(secondaryPath),
        });
    }

    public async Task<JsonElement> SetSkinToneAsync(string tone)
    {
        return await SendRuntimeRequestAsync(
            "set-skin-tone",
            "skin-tone-updated",
            requestId => new { type = "set-skin-tone", tone, requestId },
            supersedePreviousLoad: false);
    }

    public async Task<JsonElement> SetMorphAsync(int index, double weight)
    {
        return await SendRuntimeRequestAsync(
            "set-morph",
            "morph-updated",
            requestId => new { type = "set-morph", index, weight, requestId },
            supersedePreviousLoad: false);
    }

    public async Task<JsonElement> ResetMorphsAsync()
    {
        return await SendRuntimeRequestAsync(
            "reset-morphs",
            "morphs-reset",
            requestId => new { type = "reset-morphs", requestId },
            supersedePreviousLoad: false);
    }

    internal async Task<double?> ReadAnimationFrameAsync()
    {
        var core = Browser.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 is not initialized.");
        var raw = await core.ExecuteScriptAsync(
            "(() => { const runtime = window.MmdDeskPetRuntime && window.MmdDeskPetRuntime.runtime; const value = runtime && runtime.currentFrameTime; return typeof value === 'number' && Number.isFinite(value) ? value : null; })()");
        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public async Task PlayAsync()
    {
        PostCommand(new { type = "play" });
        await Task.Yield();
    }

    public async Task PauseAsync()
    {
        PostCommand(new { type = "pause" });
        await Task.Yield();
    }

    internal Task<JsonElement> WaitForRuntimeMessageAsync(string type)
    {
        var source = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_requestLock)
        {
            _eventWaiters.Add(new EventWaiter(type, source));
        }

        Trace($"request-start:event:{type}");
        return source.Task;
    }

    public async Task CapturePreviewAsync(string outputPath)
    {
        var core = Browser.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 is not initialized.");
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
    }

    internal void UseOpaqueCaptureBackgroundForTest()
    {
        Browser.DefaultBackgroundColor = System.Drawing.Color.White;
    }

    internal void UseTransparentCaptureBackgroundForTest()
    {
        Browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 0, 0, 0);
    }

    private void OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        Trace($"core-init:{e.IsSuccess}");
        if (!e.IsSuccess)
        {
            _lastRuntimeError = e.InitializationException?.ToString() ?? "WebView2 initialization failed.";
            _runtimeReady.TrySetException(new InvalidOperationException(_lastRuntimeError));
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Trace($"navigation:{e.IsSuccess}:{e.WebErrorStatus}:id={e.NavigationId}");
        if (!e.IsSuccess)
        {
            return;
        }

        var source = Browser.CoreWebView2?.Source ?? string.Empty;
        if (source.IndexOf(AppVirtualHost, StringComparison.OrdinalIgnoreCase) < 0)
        {
            Trace($"navigation-ignored:{source}");
            return;
        }

        if (_sawPetNavigation)
        {
            Trace("navigation-repeat");
            CancelPendingRequests("navigation-reset");
            return;
        }

        _sawPetNavigation = true;
        Trace("navigation-ready");
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Trace($"process-failed:{e.ProcessFailedKind}:{e.Reason}");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(type))
            {
                return;
            }
            Trace($"bridge-recv:{Truncate(e.WebMessageAsJson, 700)}");
            if (type.Equals("motion-loaded", StringComparison.Ordinal)
                && root.TryGetProperty("rootOffset", out var rootOffset))
            {
                Trace($"motion-root-offset:{rootOffset}");
            }

            if (type.Equals("console-error", StringComparison.Ordinal))
            {
                var consoleMessage = root.TryGetProperty("message", out var consoleElement)
                    ? consoleElement.GetString()
                    : string.Empty;
                Trace($"console-error:{consoleMessage}");
                return;
            }

            if (type.Equals("runtime-ready", StringComparison.Ordinal))
            {
                if (_runtimeReady.TrySetResult(true))
                {
                    FlushQueuedCommands();
                }

                return;
            }

            if (type.Equals("runtime-error", StringComparison.Ordinal))
            {
                var message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : "Unknown MMD runtime error.";
                var stack = root.TryGetProperty("stack", out var stackElement)
                    ? stackElement.GetString()
                    : string.Empty;
                _lastRuntimeError = $"{message}{Environment.NewLine}{stack}";
                var requestId = root.TryGetProperty("requestId", out var errorRequestId)
                    ? errorRequestId.GetString()
                    : null;
                Trace($"runtime-error:requestId={requestId}:{Truncate(message, 400)} stack={Truncate(stack, 400)}");
                var exception = new InvalidOperationException(message ?? "Unknown MMD runtime error.");
                _runtimeReady.TrySetException(exception);
                var failed = _requests.FailByRuntimeError(requestId, exception);
                Trace(failed.Count > 0
                    ? $"runtime-error-correlated:{requestId}:{string.Join(",", failed.Select(item => item.Operation))}"
                    : $"runtime-error-unmatched:{requestId}");
                return;
            }

            if (type.Equals("context-menu", StringComparison.Ordinal))
            {
                OpenPetMenu();
                return;
            }

            if (!TryCompletePendingRequest(type, root))
            {
                TryCompleteEventWaiter(type, root);
            }
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
            Trace($"message-parse-failure:{exception.GetBaseException().Message}");
            CancelPendingRequests("message-parse-failure");
        }
    }

    private async Task<JsonElement> SendRuntimeRequestAsync(
        string operation,
        string responseType,
        Func<string, object> commandFactory,
        bool supersedePreviousLoad)
    {
        await _requestGate.WaitAsync();
        var request = _requests.Begin(operation, responseType, supersedePreviousLoad);

        try
        {
            Trace($"request-start:{operation}:{request.RequestId}");
            PostCommand(commandFactory(request.RequestId));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, request.Cancel.Token);
            try
            {
                var result = await request.Completion.Task.WaitAsync(linked.Token);
                Trace($"request-finish:{operation}:{request.RequestId}");
                return result;
            }
            catch (OperationCanceledException) when (request.Superseded)
            {
                Trace($"request-superseded:{operation}:{request.RequestId}:{request.CancelReason}");
                throw;
            }
            catch (OperationCanceledException)
            {
                Trace($"request-timeout:{operation}:{request.RequestId}");
                throw new TimeoutException($"The MMD runtime did not finish {operation} within 30 seconds.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not TimeoutException)
        {
            Trace($"request-failure:{operation}:{request.RequestId}:{exception.GetBaseException().Message}");
            throw;
        }
        finally
        {
            _requests.Release(request);
            _requestGate.Release();
        }
    }

    private void CancelPendingRequests(string reason)
    {
        var pending = _requests.CancelAll(reason);
        List<EventWaiter> events;
        lock (_requestLock)
        {
            events = _eventWaiters.ToList();
            _eventWaiters.Clear();
        }

        Trace($"request-cancel-all:{reason}:{pending.Count}");
        foreach (var waiter in events)
        {
            waiter.Completion.TrySetException(new OperationCanceledException(reason));
        }
    }

    private bool TryCompletePendingRequest(string type, JsonElement root)
    {
        var requestId = root.TryGetProperty("requestId", out var requestIdElement)
            ? requestIdElement.GetString()
            : null;
        return _requests.TryComplete(type, requestId, root);
    }

    private void TryCompleteEventWaiter(string type, JsonElement root)
    {
        EventWaiter? waiter = null;
        lock (_requestLock)
        {
            var index = _eventWaiters.FindIndex(item =>
                string.Equals(item.Type, type, StringComparison.Ordinal));
            if (index >= 0)
            {
                waiter = _eventWaiters[index];
                _eventWaiters.RemoveAt(index);
            }
        }

        if (waiter is null)
        {
            return;
        }

        Trace($"request-finish:event:{type}");
        waiter.Completion.TrySetResult(root.Clone());
    }

    private static bool IsSuperseded(Exception exception)
    {
        return exception.GetBaseException() is OperationCanceledException;
    }

    private void PostCommand(object command)
    {
        var json = JsonSerializer.Serialize(command);
        bool sendNow;
        lock (_requestLock)
        {
            if (_runtimeReady.Task.IsFaulted || _runtimeReady.Task.IsCanceled)
            {
                throw new InvalidOperationException("The MMD runtime failed before the command was sent.");
            }

            sendNow = _runtimeReady.Task.IsCompletedSuccessfully && _commandsBeforeReady.Count == 0;
            if (!sendNow)
            {
                _commandsBeforeReady.Enqueue(json);
                Trace($"bridge-queue:{Truncate(json, 500)}");
            }
        }

        if (sendNow)
        {
            PostCommandNow(json);
            return;
        }

        if (_runtimeReady.Task.IsCompletedSuccessfully)
        {
            FlushQueuedCommands();
        }
    }

    private void FlushQueuedCommands()
    {
        while (true)
        {
            string? json;
            lock (_requestLock)
            {
                if (_commandsBeforeReady.Count == 0)
                {
                    return;
                }

                json = _commandsBeforeReady.Dequeue();
            }

            PostCommandNow(json);
        }
    }

    private void PostCommandNow(string json)
    {
        var core = Browser.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 is not initialized.");
        Trace($"bridge-send:{Truncate(json, 500)}");
        core.PostWebMessageAsJson(json);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var oneLine = value.Replace("\r", " ").Replace("\n", " ");
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }

    private void BuildBaseContextMenu()
    {
        ConfigureOpenMenu(_openModelMenu, Text("打开模型…", "Open model…"), OnOpenModelMenuClick);
        ConfigureOpenMenu(_openMotionMenu, Text("打开动作…", "Open motion…"), OnOpenMotionMenuClick);
        ConfigureOpenMenu(_openMusicMenu, Text("打开音乐…", "Open music…"), OnOpenMusicMenuClick);
        _libraryModelsMenu.Header = Text("模型", "Models");
        _libraryMotionsMenu.Header = Text("动作", "Motions");
        _libraryMusicMenu.Header = Text("音乐", "Music");
        ConfigureMenuItem(_libraryModelsMenu);
        ConfigureMenuItem(_libraryMotionsMenu);
        ConfigureMenuItem(_libraryMusicMenu);
        ConfigureOpenMenu(_openLibraryFolderMenu, Text("打开模型文件夹", "Open models folder"), OnOpenLibraryFolderClick);
        ConfigureOpenMenu(_rescanLibraryMenu, Text("重新扫描", "Rescan"), OnRescanLibraryClick);
        ConfigureRecentMenu(_recentModelsMenu, Text("最近的模型", "Recent models"));
        ConfigureRecentMenu(_recentMotionsMenu, Text("最近的动作", "Recent motions"));
        ConfigureRecentMenu(_recentMusicMenu, Text("最近的音乐", "Recent music"));

        _switchCharacterMenu.Header = Text("👤 切换人物", "👤 Switch character");
        ConfigureMenuItem(_switchCharacterMenu);
        _switchCharacterMenu.StaysOpenOnClick = true;
        _switchCharacterMenu.Click += OnSwitchCharacterMenuClick;

        _appearanceMenu.Header = Text("✨ 外观", "✨ Appearance");
        ConfigureMenuItem(_appearanceMenu);
        _appearanceMenu.IsEnabled = false;

        _skinToneMenu.Header = Text("肤色", "Skin tone");
        _skinToneMenu.Visibility = Visibility.Collapsed;
        BuildSkinToneMenu();

        _nakedMenu.Header = Text("全裸", "Full nude");
        ConfigureMenuItem(_nakedMenu);
        _nakedMenu.Visibility = Visibility.Collapsed;
        _nakedMenu.StaysOpenOnClick = true;
        _nakedMenu.Click += OnMorphMenuClick;

        _danceMenu.Header = Text("🎵 舞蹈", "🎵 Dance");
        ConfigureMenuItem(_danceMenu);
        BuildDanceMenu();

        _pauseDanceMenu.Header = Text("暂停动作", "Pause motion");
        ConfigureMenuItem(_pauseDanceMenu);
        _pauseDanceMenu.StaysOpenOnClick = true;
        _pauseDanceMenu.Click += OnPauseDanceMenuClick;
        _pauseDanceMenu.IsEnabled = false;
        _loopMotionMenu.Header = Text("循环播放", "Loop playback");
        ConfigureMenuItem(_loopMotionMenu);
        _loopMotionMenu.IsCheckable = true;
        _loopMotionMenu.IsChecked = _settings.LoopMotion;
        _loopMotionMenu.StaysOpenOnClick = true;
        _loopMotionMenu.Click += OnLoopMotionToggleClick;
        _danceMenu.Items.Add(new Separator());
        _danceMenu.Items.Add(_loopMotionMenu);
        _danceMenu.Items.Add(_pauseDanceMenu);

        _eyeExpressionMenu.Header = Text("眼睛表情", "Eye expressions");
        _mouthExpressionMenu.Header = Text("嘴部表情", "Mouth expressions");
        _clothingMenu.Header = Text("服装与鞋袜", "Clothes and footwear");
        _adultMenu.Header = Text("成人形变", "Adult morphs");
        _allMorphsMenu.Header = Text("全部形变", "All morphs");
        ConfigureMenuItem(_eyeExpressionMenu);
        ConfigureMenuItem(_mouthExpressionMenu);
        ConfigureMenuItem(_clothingMenu);
        ConfigureMenuItem(_adultMenu);
        ConfigureMenuItem(_allMorphsMenu);
        _allMorphsMenu.Visibility = Visibility.Collapsed;

        _adultContentToggle.Header = Text("显示成人与脱衣选项", "Show adult and outfit-removal options");
        ConfigureMenuItem(_adultContentToggle);
        _adultContentToggle.IsCheckable = true;
        _adultContentToggle.IsChecked = _adultContentEnabled;
        _adultContentToggle.StaysOpenOnClick = true;
        _adultContentToggle.Visibility = Visibility.Collapsed;
        _adultContentToggle.Click += OnAdultContentToggleClick;

        _resetMorphsMenu.Header = Text("恢复默认形变", "Reset morphs");
        ConfigureMenuItem(_resetMorphsMenu);
        _resetMorphsMenu.StaysOpenOnClick = true;
        _resetMorphsMenu.Click += OnResetMorphsMenuClick;
        _resetMorphsMenu.IsEnabled = false;

        _appearanceMenu.Items.Add(_skinToneMenu);
        _appearanceMenu.Items.Add(_eyeExpressionMenu);
        _appearanceMenu.Items.Add(_mouthExpressionMenu);
        _appearanceMenu.Items.Add(_allMorphsMenu);
        _appearanceMenu.Items.Add(new Separator());
        _appearanceMenu.Items.Add(_adultContentToggle);
        _appearanceMenu.Items.Add(_nakedMenu);
        _appearanceMenu.Items.Add(_clothingMenu);
        _appearanceMenu.Items.Add(_adultMenu);
        _appearanceMenu.Items.Add(new Separator());
        _appearanceMenu.Items.Add(_resetMorphsMenu);

        _alwaysOnTopMenu.Header = Text("📌 始终置顶", "📌 Always on top");
        ConfigureMenuItem(_alwaysOnTopMenu);
        _alwaysOnTopMenu.IsCheckable = true;
        _alwaysOnTopMenu.IsChecked = true;
        _alwaysOnTopMenu.StaysOpenOnClick = true;
        _alwaysOnTopMenu.Click += OnAlwaysOnTopMenuClick;

        _closeMenu.Header = Text("关闭桌宠", "Close desktop pet");
        ConfigureMenuItem(_closeMenu);
        _closeMenu.Click += OnCloseMenuClick;

        _petMenu.Items.Add(_openModelMenu);
        _petMenu.Items.Add(_libraryModelsMenu);
        _petMenu.Items.Add(_openMotionMenu);
        _petMenu.Items.Add(_libraryMotionsMenu);
        _petMenu.Items.Add(_openMusicMenu);
        _petMenu.Items.Add(_libraryMusicMenu);
        _petMenu.Items.Add(_recentModelsMenu);
        _petMenu.Items.Add(_recentMotionsMenu);
        _petMenu.Items.Add(_recentMusicMenu);
        _petMenu.Items.Add(_openLibraryFolderMenu);
        _petMenu.Items.Add(_rescanLibraryMenu);
        _petMenu.Items.Add(new Separator());
        _petMenu.Items.Add(_switchCharacterMenu);
        _petMenu.Items.Add(_danceMenu);
        _petMenu.Items.Add(_appearanceMenu);
        _petMenu.Items.Add(_alwaysOnTopMenu);
        _petMenu.Items.Add(new Separator());
        _petMenu.Items.Add(_closeMenu);
        RefreshRecentMenus();
        UpdateSwitchCharacterMenu();
        if (IsExistingFile(_settings.LastMotionPath))
        {
            UpdateCustomMotionMenu(_settings.LastMotionPath);
        }
        _petMenu.PlacementTarget = this;
        _petMenu.Opened += OnPetMenuOpened;
        _petMenu.Closed += OnPetMenuClosed;
        ContextMenu = _petMenu;
    }

    private void BuildSkinToneMenu()
    {
        var options = new[]
        {
            (Key: "brighter", Header: Text("默认", "Default")),
            (Key: "lighter", Header: Text("稍深", "Slightly darker")),
            (Key: "deep", Header: Text("更深", "Darker")),
        };

        foreach (var option in options)
        {
            var item = new MenuItem
            {
                Header = option.Header,
                Tag = option.Key,
                IsCheckable = false,
                StaysOpenOnClick = true,
            };
            ConfigureMenuItem(item);
            SetRadioIndicator(item, selected: false);
            item.Click += OnSkinToneMenuClick;
            _skinToneMenu.Items.Add(item);
            _skinToneMenuItems[option.Key] = item;
        }

        SetSkinToneMenuSelection("brighter");
    }

    private void BuildDanceMenu()
    {
        var availableCount = 0;
        foreach (var dance in _danceMotions)
        {
            var path = GetDanceMotionPath(dance);
            var secondaryPath = GetSecondaryDanceMotionPath(dance);
            var isAvailable = File.Exists(path)
                && (secondaryPath is null || File.Exists(secondaryPath));
            if (dance.IsDual
                && (!File.Exists(_modelPaths[0]) || !File.Exists(_modelPaths[1])))
            {
                isAvailable = false;
            }

            if (!isAvailable)
            {
                continue;
            }

            var musicPath = GetDanceMusicPath(dance);

            availableCount++;
            var item = new MenuItem
            {
                Header = Text(dance.ChineseName, dance.EnglishName)
                    + (musicPath is null ? string.Empty : Text("（有本地音乐）", " (local music)")),
                Tag = dance,
                IsEnabled = true,
                IsCheckable = false,
                StaysOpenOnClick = true,
            };
            ConfigureMenuItem(item);
            SetRadioIndicator(item, selected: false);
            _danceMenuItems.Add(item);
            var tooltipParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(dance.UsageNote))
            {
                tooltipParts.Add(dance.UsageNote);
            }

            if (musicPath is not null)
            {
                tooltipParts.Add(Text(
                    $"音乐：{Path.GetRelativePath(_repositoryRoot, musicPath).Replace('\\', '/')}",
                    $"Music: {Path.GetRelativePath(_repositoryRoot, musicPath).Replace('\\', '/')}"));
            }
            else if (item.IsEnabled)
            {
                tooltipParts.Add(Text(
                    "未附带音乐；可将与动作同名的 mp3、wav、ogg 或 m4a 放入 assets/music",
                    "No track is bundled; add a same-name mp3, wav, ogg, or m4a file to assets/music"));
            }

            if (tooltipParts.Count > 0)
            {
                item.ToolTip = string.Join(Environment.NewLine, tooltipParts);
            }

            item.Click += OnDanceMotionMenuClick;
            _builtInDanceMenu.Items.Add(item);
        }

        _builtInDanceMenu.Header = Text("内置示例", "Built-in examples");
        ConfigureMenuItem(_builtInDanceMenu);
        _builtInDanceMenu.Visibility = availableCount == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        _customMotionMenu.Header = Text("自定义动作", "Custom motion");
        ConfigureMenuItem(_customMotionMenu);
        _customMotionMenu.StaysOpenOnClick = true;
        _customMotionMenu.Visibility = Visibility.Collapsed;
        _customMotionMenu.Click += OnCustomMotionMenuClick;
        SetRadioIndicator(_customMotionMenu, selected: false);
        _danceMenuItems.Add(_customMotionMenu);

        _noDanceMenu.Header = Text("暂无可用舞蹈", "No dance motion is available");
        _noDanceMenu.IsEnabled = false;
        ConfigureMenuItem(_noDanceMenu);
        _noDanceMenu.Visibility = availableCount == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _danceMenu.Items.Add(_customMotionMenu);
        _danceMenu.Items.Add(_builtInDanceMenu);
        _danceMenu.Items.Add(_noDanceMenu);
    }

    private async void OnSkinToneMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tone })
        {
            return;
        }

        SetSkinToneMenuSelection(tone);
        try
        {
            await SetSkinToneAsync(tone);
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
        }
    }

    private void SetSkinToneMenuSelection(string tone)
    {
        foreach (var item in _skinToneMenuItems)
        {
            SetRadioIndicator(
                item.Value,
                string.Equals(item.Key, tone, StringComparison.Ordinal));
        }
    }

    private void PopulateMorphMenus(JsonElement message)
    {
        _morphMenuItems.Clear();
        _activeMorphBySubmenu.Clear();
        _nakedMenu.Tag = null;
        _nakedMenu.Visibility = Visibility.Collapsed;
        SetRadioIndicator(_nakedMenu, selected: false);
        ClearMenu(_eyeExpressionMenu);
        ClearMenu(_mouthExpressionMenu);
        ClearMenu(_clothingMenu);
        ClearMenu(_adultMenu);
        ClearMenu(_allMorphsMenu);

        var eyeExpressions = new List<(int Index, string Name)>();
        var mouthExpressions = new List<(int Index, string Name)>();
        var clothing = new List<(int Index, string Name)>();
        var adult = new List<(int Index, string Name)>();
        var allMorphs = new List<(int Index, string Name)>();
        var inAdultSection = false;

        foreach (var morph in message.GetProperty("morphs").EnumerateArray())
        {
            var index = morph.GetProperty("index").GetInt32();
            var name = morph.GetProperty("name").GetString() ?? $"Morph {index}";
            var englishName = morph.TryGetProperty("englishName", out var englishNameElement)
                ? englishNameElement.GetString() ?? string.Empty
                : string.Empty;
            var nameLower = name.ToLowerInvariant();

            if (name.StartsWith("--", StringComparison.Ordinal))
            {
                inAdultSection = name.Contains("r18", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("adult", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("成人", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (IsInternalMorph(name, englishName))
            {
                continue;
            }

            if (IsFullNudeMorph(name, englishName))
            {
                _nakedMenu.Tag = index;
                _morphMenuItems[index] = _nakedMenu;
                continue;
            }

            if (MorphCatalog.IsEyeExpression(name, englishName))
            {
                eyeExpressions.Add((index, MorphHeader(name, englishName)));
                continue;
            }

            if (MorphCatalog.IsMouthExpression(name, englishName))
            {
                mouthExpressions.Add((index, MorphHeader(name, englishName)));
                continue;
            }

            var isAdult = inAdultSection
                || nameLower.Contains("r18", StringComparison.Ordinal)
                || nameLower.Contains("adult", StringComparison.Ordinal)
                || name.Contains("成人", StringComparison.Ordinal);
            var isClothing = ContainsAny(nameLower,
                "服", "衣", "裙", "裤", "鞋", "袜", "靴", "裸足", "ストッキング", "ソックス", "パンツ",
                "スカート", "チョーカー", "タンク", "胸はだけ", "スリップ", "チューブ", "t-バック", "tバック",
                "水着", "下着", "cloth", "wear", "shoes", "stocking", "pants");

            if (isAdult || IsUsefulAdultMorph(name, englishName, isClothing: false))
            {
                if (IsUsefulAdultMorph(name, englishName, isClothing))
                {
                    adult.Add((index, MorphHeader(name, englishName)));
                }

                continue;
            }

            if (isClothing)
            {
                clothing.Add((index, MorphHeader(name, englishName)));
                continue;
            }

            allMorphs.Add((index, MorphHeader(name, englishName)));
        }

        AddMorphItems(_eyeExpressionMenu, eyeExpressions);
        AddMorphItems(_mouthExpressionMenu, mouthExpressions);
        AddMorphItems(_clothingMenu, clothing);
        AddMorphItems(_adultMenu, adult);
        AddMorphItems(_allMorphsMenu, allMorphs);

        _eyeExpressionMenu.Visibility = eyeExpressions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _mouthExpressionMenu.Visibility = mouthExpressions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _allMorphsMenu.Visibility = allMorphs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _modelHasNakedMorph = _nakedMenu.Tag is int;
        _modelHasAdultMorphs = adult.Count > 0;
        _modelHasClothingMorphs = clothing.Count > 0;
        UpdateSkinToneMenu(message);
        ApplySensitiveMenuVisibility();
    }

    private void AddMorphItems(MenuItem parent, IEnumerable<(int Index, string Name)> morphs)
    {
        foreach (var morph in morphs)
        {
            var item = new MenuItem
            {
                Header = morph.Name,
                Tag = morph.Index,
                IsCheckable = false,
                StaysOpenOnClick = true,
            };
            ConfigureMenuItem(item);
            SetRadioIndicator(item, selected: false);
            item.Click += OnMorphMenuClick;
            parent.Items.Add(item);
            _morphMenuItems[morph.Index] = item;
        }
    }

    private async void OnMorphMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not int index)
        {
            return;
        }

        await ApplyMorphSelectionAsync(item, index);
    }

    internal bool FullNudeActiveForTest => _fullNudeActive;
    internal int ActiveEyeExpressionCountForTest =>
        _activeMorphBySubmenu.ContainsKey(_eyeExpressionMenu) ? 1 : 0;
    internal int? ActiveEyeExpressionIndexForTest =>
        _activeMorphBySubmenu.TryGetValue(_eyeExpressionMenu, out var index) ? index : null;
    internal int ActiveMouthExpressionCountForTest =>
        _activeMorphBySubmenu.ContainsKey(_mouthExpressionMenu) ? 1 : 0;
    internal int? ActiveMouthExpressionIndexForTest =>
        _activeMorphBySubmenu.TryGetValue(_mouthExpressionMenu, out var index) ? index : null;
    internal bool IsCuratedEyeMorphForTest(int index)
    {
        return _morphMenuItems.TryGetValue(index, out var item)
            && ReferenceEquals(GetMorphSubmenu(item), _eyeExpressionMenu);
    }
    internal bool IsCuratedMouthMorphForTest(int index)
    {
        return _morphMenuItems.TryGetValue(index, out var item)
            && ReferenceEquals(GetMorphSubmenu(item), _mouthExpressionMenu);
    }

    internal async Task SelectMorphForTestAsync(int index)
    {
        if (!_morphMenuItems.TryGetValue(index, out var item))
        {
            throw new InvalidDataException($"Morph index {index} was not present in the curated menu.");
        }

        await ApplyMorphSelectionAsync(item, index);
    }

    private async Task ApplyMorphSelectionAsync(MenuItem item, int index)
    {
        if (ReferenceEquals(item, _nakedMenu))
        {
            await ApplyFullNudeSelectionAsync(index);
            return;
        }

        var submenu = GetMorphSubmenu(item);
        if (submenu is null)
        {
            return;
        }

        var hadPrevious = _activeMorphBySubmenu.TryGetValue(submenu, out var previousIndex);
        var selectingClothing = ReferenceEquals(submenu, _clothingMenu);
        var wasFullNudeActive = _fullNudeActive;
        var nakedMorphIndex = _nakedMenu.Tag is int value ? value : -1;

        try
        {
            if (selectingClothing && _fullNudeActive && nakedMorphIndex >= 0)
            {
                await SetMorphAsync(nakedMorphIndex, 0.0);
                _fullNudeActive = false;
            }

            if (hadPrevious && previousIndex != index)
            {
                await SetMorphAsync(previousIndex, 0.0);
            }

            await SetMorphAsync(index, 1.0);
            _activeMorphBySubmenu[submenu] = index;
            UpdateMorphRadioIndicators();
        }
        catch (Exception exception)
        {
            _fullNudeActive = wasFullNudeActive;
            if (hadPrevious && previousIndex != index)
            {
                try
                {
                    await SetMorphAsync(previousIndex, 1.0);
                }
                catch
                {
                    // Keep the original runtime error as the user-facing failure.
                }
            }

            if (selectingClothing && wasFullNudeActive && nakedMorphIndex >= 0)
            {
                try
                {
                    await SetMorphAsync(nakedMorphIndex, 1.0);
                }
                catch
                {
                    // Keep the original runtime error as the user-facing failure.
                }
            }

            UpdateMorphRadioIndicators();
            _lastRuntimeError = exception.ToString();
        }
    }

    private async Task ApplyFullNudeSelectionAsync(int index)
    {
        var hadClothing = _activeMorphBySubmenu.TryGetValue(_clothingMenu, out var clothingIndex);
        var wasFullNudeActive = _fullNudeActive;
        try
        {
            if (hadClothing)
            {
                await SetMorphAsync(clothingIndex, 0.0);
                _activeMorphBySubmenu.Remove(_clothingMenu);
            }

            await SetMorphAsync(index, 1.0);
            _fullNudeActive = true;
            UpdateMorphRadioIndicators();
        }
        catch (Exception exception)
        {
            _fullNudeActive = wasFullNudeActive;
            if (hadClothing)
            {
                try
                {
                    await SetMorphAsync(clothingIndex, 1.0);
                    _activeMorphBySubmenu[_clothingMenu] = clothingIndex;
                }
                catch
                {
                    // Keep the original runtime error as the user-facing failure.
                }
            }

            UpdateMorphRadioIndicators();
            _lastRuntimeError = exception.ToString();
        }
    }

    private MenuItem? GetMorphSubmenu(MenuItem item)
    {
        if (_eyeExpressionMenu.Items.Contains(item))
        {
            return _eyeExpressionMenu;
        }

        if (_mouthExpressionMenu.Items.Contains(item))
        {
            return _mouthExpressionMenu;
        }

        if (_clothingMenu.Items.Contains(item))
        {
            return _clothingMenu;
        }

        if (_adultMenu.Items.Contains(item))
        {
            return _adultMenu;
        }

        if (_allMorphsMenu.Items.Contains(item))
        {
            return _allMorphsMenu;
        }

        return null;
    }

    private void OnSwitchCharacterMenuClick(object? sender, RoutedEventArgs e)
    {
        _ = SwitchCharacterAsync();
    }

    private async void OnDanceMotionMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not DanceMotionDefinition dance)
        {
            return;
        }

        if (!dance.IsDual && !_modelLoaded)
        {
            ShowInfo(Text(
                "请先打开或拖入一个 PMX 模型。",
                "Open or drop a PMX model first."));
            return;
        }

        var previousDance = _activeDanceMenuItem;
        foreach (var danceItem in _danceMenuItems)
        {
            SetRadioIndicator(danceItem, ReferenceEquals(danceItem, item));
        }

        _danceMenu.IsEnabled = false;
        _switchCharacterMenu.IsEnabled = false;
        try
        {
            JsonElement motion;
            if (dance.IsDual)
            {
                var secondaryPath = GetSecondaryDanceMotionPath(dance)
                    ?? throw new InvalidDataException("The dual dance has no secondary VMD path.");
                BeginDualMotionPreload(dance);
                if (!_dualMode)
                {
                    if (_currentCharacter == 0)
                    {
                        await LoadSecondaryCharacterAsync(0, 1);
                    }
                    else
                    {
                        await LoadDualCharactersAsync(0, 1);
                    }
                }

                motion = await LoadDualMotionAsync(
                    dance.Key,
                    GetDanceMotionPath(dance),
                    secondaryPath,
                    dance.StartFrame);
                if (motion.GetProperty("primaryBoneTracks").GetInt32() <= 0
                    || motion.GetProperty("secondaryBoneTracks").GetInt32() <= 0)
                {
                    throw new InvalidDataException("The selected dual VMD motion contained no bone tracks.");
                }
            }
            else
            {
                if (_dualMode)
                {
                    await LoadCharacterAsync(_currentCharacter);
                }

                motion = await LoadMotionAsync(
                    dance.Key,
                    GetDanceMotionPath(dance),
                    dance.Loop);
                if (motion.GetProperty("boneTracks").GetInt32() <= 0)
                {
                    throw new InvalidDataException("The selected VMD contained no bone tracks.");
                }
            }

            await LoadDanceMusicAsync(dance);
            await PlayAsync();
            _userMotionActive = false;
            _activeDanceMenuItem = item;
            _pauseDanceMenu.IsEnabled = true;
        }
        catch (Exception exception)
        {
            SetRadioIndicator(item, selected: false);
            if (previousDance is not null)
            {
                SetRadioIndicator(previousDance, selected: true);
            }

            _activeDanceMenuItem = previousDance;
            _lastRuntimeError = exception.ToString();
        }
        finally
        {
            _danceMenu.IsEnabled = true;
            _switchCharacterMenu.IsEnabled = true;
        }
    }

    private async void OnLoopMotionToggleClick(object sender, RoutedEventArgs e)
    {
        _settings.LoopMotion = _loopMotionMenu.IsChecked == true;
        UserSettingsStore.Save(_settings);
        if (!_userMotionActive || !_modelLoaded)
        {
            return;
        }

        try
        {
            await SetMotionLoopAsync(_settings.LoopMotion);
        }
        catch (Exception exception) when (IsSuperseded(exception))
        {
            Trace($"action-superseded:{exception.GetBaseException().Message}");
        }
        catch (Exception exception)
        {
            ShowActionError(exception);
        }
    }

    private async void OnPauseDanceMenuClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await PauseAsync();
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
        }
    }

    private async Task SwitchCharacterAsync()
    {
        if (!_danceMenu.IsEnabled)
        {
            return;
        }

        _switchCharacterMenu.IsEnabled = false;
        try
        {
            await LoadCharacterAsync((_currentCharacter + 1) % _modelPaths.Length);
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
        }
        finally
        {
            _switchCharacterMenu.IsEnabled = true;
        }
    }

    private async void OnResetMorphsMenuClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ResetMorphStateAsync();
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
        }
    }

    private void OnAlwaysOnTopMenuClick(object? sender, RoutedEventArgs e)
    {
        Topmost = _alwaysOnTopMenu.IsChecked;
    }

    private void OnCloseMenuClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _closing)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The pointer can be released between the preview event and DragMove.
        }

        e.Handled = true;
    }

    private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        OpenPetMenu();
        e.Handled = true;
    }

    private void OpenPetMenu()
    {
        if (!IsLoaded || _closing)
        {
            return;
        }

        _petMenu.PlacementTarget = this;
        _petMenu.IsOpen = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ActivatePetFocus()
    {
        Focus();
        Keyboard.Focus(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Trace("closed");
        _closing = true;
        StopLibraryWatch();
        CancelPendingRequests("window-closed");
        _petMenu.IsOpen = false;
        if (Browser is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void OnPetHostWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // Log before any other work. The previous handler returned with no
        // trace when the folder mapping swallowed the request.
        var requestUri = e.Request?.Uri ?? string.Empty;
        Trace($"resource-enter:{Truncate(requestUri, 500)}");
        CoreWebView2? core = null;
        try
        {
            core = sender as CoreWebView2 ?? Browser.CoreWebView2;
            if (core is null)
            {
                Trace("resource-fault:webview-missing");
                return;
            }

            if (!Uri.TryCreate(requestUri, UriKind.Absolute, out var uri)
                || !uri.Host.Equals(AppVirtualHost, StringComparison.OrdinalIgnoreCase))
            {
                Trace($"resource-ignore:{Truncate(requestUri, 300)}");
                return;
            }

            string? filePath;
            lock (_requestLock)
            {
                filePath = ResolvePetHostFile(uri.AbsolutePath);
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Trace($"resource-status:404:0:{Truncate(requestUri, 300)}:resolved={filePath}");
                e.Response = AssetResponse(core, new MemoryStream(), 404, "Not Found", "text/plain");
                return;
            }

            // Stay on this handler thread. A deferral that completes on a
            // background thread makes CreateWebResourceResponse fail and the
            // XHR reports status 0. WebView2 reads the stream after we return
            // and closes it, so the stream must stay open and must not be copied.
            var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                64 * 1024,
                FileOptions.SequentialScan);
            var contentType = RuntimeAssetUrls.ContentTypeFor(filePath);
            Trace($"resource-status:200:{stream.Length}:{contentType}:{filePath}");
            e.Response = AssetResponse(core, stream, 200, "OK", contentType);
        }
        catch (Exception exception)
        {
            Trace($"resource-fault:{exception.GetBaseException().Message}");
            if (core is not null)
            {
                try
                {
                    e.Response = AssetResponse(core, new MemoryStream(), 500, "Error", "text/plain");
                }
                catch (Exception responseException)
                {
                    Trace($"resource-fault-response:{responseException.GetBaseException().Message}");
                }
            }
        }
    }

    private string? ResolvePetHostFile(string absolutePath)
    {
        if (RuntimeAssetUrls.TryReadExternal(absolutePath, out _, out _))
        {
            return _externalFiles.TryResolve(absolutePath, out var externalPath) ? externalPath : null;
        }

        if (!RuntimeAssetUrls.TryReadAppRelative(absolutePath, out var relative))
        {
            return null;
        }

        var combined = Path.GetFullPath(Path.Combine(
            _repositoryRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        return ExternalFileMap.IsInsideDirectory(_repositoryRoot, combined) ? combined : null;
    }

    private static CoreWebView2WebResourceResponse AssetResponse(
        CoreWebView2 core,
        Stream content,
        int status,
        string reason,
        string contentType)
    {
        var headers = $"Content-Type: {contentType}\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store\r\n";
        return core.Environment.CreateWebResourceResponse(content, status, reason, headers);
    }

    private string ToRuntimeUrl(string absolutePath)
    {
        var fullPath = Path.GetFullPath(absolutePath);
        if (ExternalFileMap.IsInsideDirectory(_repositoryRoot, fullPath))
        {
            return RuntimeAssetUrls.ForAppFile(Path.GetRelativePath(_repositoryRoot, fullPath));
        }

        string url;
        string token;
        bool created;
        lock (_requestLock)
        {
            url = _externalFiles.UrlForFile(fullPath, out token, out created);
        }

        if (created)
        {
            var directory = Path.GetDirectoryName(fullPath) ?? fullPath;
            Trace($"external-map:{token}->{directory}");
        }

        Trace($"external-url:{url}");
        return url;
    }

    private string ResolveModelPath(string modelDirectory, string fileName)
    {
        var candidates = new List<string>
        {
            Path.Combine(_repositoryRoot, "assets", "Hime_&_Mikoto", modelDirectory, fileName),
            Path.Combine(_repositoryRoot, "HimeMikotoDesktop", "assets", "Hime_&_Mikoto", modelDirectory, fileName),
        };
        var parent = Directory.GetParent(_repositoryRoot);
        if (parent is not null)
        {
            candidates.Add(Path.Combine(
                parent.FullName,
                "HimeMikotoDesktop",
                "assets",
                "Hime_&_Mikoto",
                modelDirectory,
                fileName));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private string ResolveProjectPath(string relativePath)
    {
        var sourceLayoutPath = Path.Combine(_repositoryRoot, relativePath);
        if (File.Exists(sourceLayoutPath) || Directory.Exists(sourceLayoutPath))
        {
            return sourceLayoutPath;
        }

        const string nativeProjectPrefix = "HimeMikotoDesktopNative";
        if (relativePath.Equals(nativeProjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return _repositoryRoot;
        }

        var portableRelativePath = relativePath.StartsWith(
            nativeProjectPrefix + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)
            ? relativePath[(nativeProjectPrefix.Length + 1)..]
            : relativePath.StartsWith(
                nativeProjectPrefix + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
                ? relativePath[(nativeProjectPrefix.Length + 1)..]
                : relativePath;
        return Path.Combine(_repositoryRoot, portableRelativePath);
    }

    private string Text(string chinese, string english)
    {
        return _english ? english : chinese;
    }

    private static void ClearMenu(MenuItem menu)
    {
        menu.Items.Clear();
    }

    private async Task ApplyFullNudeIfNeededAsync()
    {
        if (!_fullNudeActive || _nakedMenu.Tag is not int nakedMorphIndex)
        {
            return;
        }

        await SetMorphAsync(nakedMorphIndex, 1.0);
        SetRadioIndicator(_nakedMenu, selected: true);
    }

    internal Task ClearMorphStateForTestAsync()
    {
        return ResetMorphStateAsync();
    }

    private async Task ResetMorphStateAsync()
    {
        _fullNudeActive = false;
        _activeMorphBySubmenu.Clear();
        foreach (var item in _morphMenuItems.Values.Distinct())
        {
            SetRadioIndicator(item, selected: false);
        }

        await ResetMorphsAsync();
    }

    private void UpdateMorphRadioIndicators()
    {
        foreach (var item in _morphMenuItems.Values.Distinct())
        {
            SetRadioIndicator(item, IsActiveMorphItem(item));
        }
    }

    private bool IsActiveMorphItem(MenuItem item)
    {
        if (ReferenceEquals(item, _nakedMenu))
        {
            return _fullNudeActive;
        }

        if (item.Tag is not int index)
        {
            return false;
        }

        var submenu = GetMorphSubmenu(item);
        return submenu is not null
            && _activeMorphBySubmenu.TryGetValue(submenu, out var activeIndex)
            && activeIndex == index;
    }

    private void AddDisabledItem(MenuItem parent, string header)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = false,
        };
        ConfigureMenuItem(item);
        parent.Items.Add(item);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(value.Contains);
    }

    private static void SetRadioIndicator(MenuItem item, bool selected)
    {
        if (item.Icon is not TextBlock indicator)
        {
            indicator = new TextBlock
            {
                Width = 18,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(125, 49, 94)),
            };
            item.Icon = indicator;
        }

        indicator.Text = selected ? "●" : "○";
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private void ConfigureMenuItem(MenuItem item)
    {
        item.PreviewMouseMove += OnMenuItemPreviewMouseMove;
        item.MouseEnter += OnMenuItemMouseEnter;
        item.MouseLeave += OnMenuItemMouseLeave;
        item.SubmenuOpened += OnMenuItemSubmenuOpened;
        item.SubmenuClosed += OnMenuItemSubmenuClosed;
    }

    private static void OnMenuItemMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is MenuItem item && item.IsEnabled)
        {
            ApplyMenuHover(item);
        }
    }

    private void OnMenuItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is MenuItem item && item.IsEnabled)
        {
            if (!ReferenceEquals(_hoveredMenuItem, item))
            {
                if (_hoveredMenuItem is not null)
                {
                    ClearMenuHover(_hoveredMenuItem);
                }

                _hoveredMenuItem = item;
            }

            ApplyMenuHover(item);
        }
    }

    private static void OnMenuItemMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is MenuItem item && !item.IsSubmenuOpen)
        {
            ClearMenuHover(item);
        }
    }

    private static void OnMenuItemSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.IsEnabled)
        {
            ApplyMenuHover(item);
        }
    }

    private static void OnMenuItemSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && !item.IsMouseOver)
        {
            ClearMenuHover(item);
        }
    }

    private static void ApplyMenuHover(MenuItem item)
    {
        item.Background = MenuHoverBackground;
        item.Foreground = MenuHoverForeground;
    }

    private static void ClearMenuHover(MenuItem item)
    {
        item.ClearValue(MenuItem.BackgroundProperty);
        item.ClearValue(MenuItem.ForegroundProperty);
    }

    private void OnPetMenuOpened(object? sender, RoutedEventArgs e)
    {
        _hoveredMenuItem = null;
    }

    private void OnPetMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (_hoveredMenuItem is not null)
        {
            ClearMenuHover(_hoveredMenuItem);
            _hoveredMenuItem = null;
        }
    }

    private static bool IsInternalMorph(string name, string englishName)
    {
        return name.StartsWith("├", StringComparison.Ordinal)
            || name.StartsWith("└", StringComparison.Ordinal)
            || name.Contains("__", StringComparison.Ordinal)
            || englishName.Contains("__", StringComparison.Ordinal);
    }

    private string MorphHeader(string name, string englishName)
    {
        if (_english)
        {
            return string.IsNullOrWhiteSpace(englishName) ? name : englishName;
        }

        if (ChineseMorphNames.TryGetValue(englishName, out var chineseName))
        {
            return chineseName;
        }

        if (!string.IsNullOrWhiteSpace(englishName)
            && englishName.Any(char.IsLetter))
        {
            return englishName
                .Replace("x_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("__", string.Empty, StringComparison.Ordinal);
        }

        return name;
    }

    private static bool IsFullNudeMorph(string name, string englishName)
    {
        return ContainsAny(
            name.ToLowerInvariant(),
            "全裸",
            "naked",
            "nude")
            || ContainsAny(englishName.ToLowerInvariant(), "castoff", "naked", "nude");
    }

    private static bool IsUsefulAdultMorph(string name, string englishName, bool isClothing)
    {
        if (isClothing)
        {
            return true;
        }

        var combined = $"{name.ToLowerInvariant()} {englishName.ToLowerInvariant()}";
        return ContainsAny(
            combined,
            "口淫",
            "blowjob",
            "乳首",
            "nipple",
            "腹ボコ",
            "腹パン",
            "腹ゴリ",
            "boko",
            "陰唇開",
            "labopen",
            "vagopen",
            "ｱﾅﾙ開",
            "アナル開",
            "anaopen");
    }

    private static bool IsChineseSystemLanguage()
    {
        try
        {
            const uint muiLanguageName = 0x8;
            uint languageCount = 0;
            uint bufferLength = 0;
            if (GetUserPreferredUILanguages(
                    muiLanguageName,
                    ref languageCount,
                    null,
                    ref bufferLength)
                && bufferLength > 0)
            {
                var buffer = new StringBuilder((int)bufferLength);
                if (GetUserPreferredUILanguages(
                        muiLanguageName,
                        ref languageCount,
                        buffer,
                        ref bufferLength))
                {
                    var preferredLanguage = buffer
                        .ToString()
                        .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(preferredLanguage))
                    {
                        return preferredLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
        catch (DllNotFoundException)
        {
            // Non-Windows test hosts use the culture fallback below.
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows versions use the culture fallback below.
        }

        var installedLanguage = CultureInfo.InstalledUICulture.Name;
        if (!string.IsNullOrWhiteSpace(installedLanguage))
        {
            return installedLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("zh", StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserPreferredUILanguages(
        uint dwFlags,
        ref uint pulNumLanguages,
        StringBuilder? pwszLanguagesBuffer,
        ref uint pcchLanguagesBuffer);

    private void Trace(string message)
    {
        try
        {
            File.AppendAllText(
                TracePath,
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never interfere with the desktop pet.
        }
    }

    internal async Task<JsonElement> LoadModelFileAsync(
        string path,
        int? characterIndex = null,
        bool recordRecent = true)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The PMX model was not found.", path);
        }

        var index = characterIndex ?? CharacterIndexForPath(path);
        var url = ToRuntimeUrl(path);
        var message = await SendRuntimeRequestAsync(
            "load-character",
            "model-loaded",
            requestId => new
            {
                type = "load-character",
                index,
                url,
                requestId,
            },
            supersedePreviousLoad: true);
        PopulateMorphMenus(message);
        MarkSingleModelLoaded(path, index);
        if (recordRecent)
        {
            RememberModel(path);
        }

        await ApplyFullNudeIfNeededAsync();
        return message;
    }

    internal async Task<JsonElement> LoadMotionFileAsync(string path, bool recordRecent = true)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The VMD motion was not found.", path);
        }

        if (recordRecent)
        {
            RememberMotion(path);
        }

        UpdateCustomMotionMenu(path);
        if (!_modelLoaded)
        {
            ShowInfo(Text(
                "请先打开或拖入一个 PMX 模型。这个动作已记在最近使用里。",
                "Open or drop a PMX model first. This motion was saved to the recent list."));
            return default;
        }

        if (_dualMode)
        {
            if (_usingCustomModel && _activeModelPath is not null)
            {
                await LoadModelFileAsync(_activeModelPath, recordRecent: false);
            }
            else
            {
                await LoadCharacterAsync(_currentCharacter);
            }
        }

        var motion = await LoadMotionAsync(
            Path.GetFileNameWithoutExtension(path),
            path,
            loop: _settings.LoopMotion);
        if (motion.GetProperty("boneTracks").GetInt32() <= 0)
        {
            throw new InvalidDataException("The selected VMD contained no bone tracks.");
        }

        _userMotionActive = true;
        await PlayAsync();
        foreach (var danceItem in _danceMenuItems)
        {
            SetRadioIndicator(danceItem, ReferenceEquals(danceItem, _customMotionMenu));
        }

        _activeDanceMenuItem = _customMotionMenu;
        _pauseDanceMenu.IsEnabled = true;
        return motion;
    }

    private async Task SetMotionLoopAsync(bool loop)
    {
        await SendRuntimeRequestAsync(
            "set-motion-loop",
            "motion-loop-set",
            requestId => new
            {
                type = "set-motion-loop",
                loop,
                requestId,
            },
            supersedePreviousLoad: false);
    }

    private async Task LoadMusicFileAsync(string path, bool recordRecent = true)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The music file was not found.", path);
        }

        var url = ToRuntimeUrl(path);
        await SendRuntimeRequestAsync(
            "load-music",
            "music-loaded",
            requestId => new { type = "load-music", url, requestId },
            supersedePreviousLoad: false);
        if (recordRecent)
        {
            RememberMusic(path);
        }
    }

    private void MarkSingleModelLoaded(string path, int characterIndex)
    {
        _currentCharacter = characterIndex;
        _activeModelPath = path;
        _modelLoaded = true;
        _userMotionActive = false;
        _usingCustomModel = !IsBundledModelPath(path);
        _dualMode = false;
        Width = SinglePetWidth;
        _pauseDanceMenu.IsEnabled = false;
        _appearanceMenu.IsEnabled = true;
        _resetMorphsMenu.IsEnabled = true;
        HideEmptyHint();
        UpdateSwitchCharacterMenu();
    }

    private void MarkDualModelsLoaded(string primaryPath, int primaryIndex)
    {
        _currentCharacter = primaryIndex;
        _activeModelPath = primaryPath;
        _modelLoaded = true;
        _userMotionActive = false;
        _usingCustomModel = false;
        _dualMode = true;
        Width = DualPetWidth;
        _pauseDanceMenu.IsEnabled = false;
        _appearanceMenu.IsEnabled = true;
        _resetMorphsMenu.IsEnabled = true;
        HideEmptyHint();
        UpdateSwitchCharacterMenu();
    }

    private void ShowEmptyHint()
    {
        EmptyHintText.Text = Text(
            "拖入 PMX，或放进 models 文件夹",
            "Drop a PMX, or put it in the models folder");
        EmptyHint.Visibility = Visibility.Visible;
    }

    private void HideEmptyHint()
    {
        EmptyHint.Visibility = Visibility.Collapsed;
    }

    private void ConfigureOpenMenu(MenuItem item, string header, RoutedEventHandler onClick)
    {
        item.Header = header;
        ConfigureMenuItem(item);
        item.Click += onClick;
    }

    private void ConfigureRecentMenu(MenuItem item, string header)
    {
        item.Header = header;
        ConfigureMenuItem(item);
        item.Visibility = Visibility.Collapsed;
    }

    private string CurrentLibraryRoot()
    {
        var configured = !string.IsNullOrWhiteSpace(_launch.LibraryFolder)
            ? _launch.LibraryFolder
            : _settings.LibraryFolder;
        return ModelLibraryScanner.ResolveRoot(configured, ModelLibraryScanner.ExeDirectory());
    }

    private void StartLibraryWatch()
    {
        StopLibraryWatch();
        string root;
        try
        {
            root = CurrentLibraryRoot();
            ModelLibraryScanner.EnsureFolders(root);
        }
        catch (Exception exception)
        {
            Trace($"library-folders:{exception.GetBaseException().Message}");
            RefreshLibraryMenus();
            return;
        }

        Trace($"library-root:{root}");
        foreach (var folder in ModelLibraryScanner.LibraryDirectories(root))
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                watcher.Created += OnLibraryChanged;
                watcher.Deleted += OnLibraryChanged;
                watcher.Changed += OnLibraryChanged;
                watcher.Renamed += OnLibraryChanged;
                watcher.Error += OnLibraryWatcherError;
                _libraryWatchers.Add(watcher);
            }
            catch (Exception exception)
            {
                Trace($"library-watch:{exception.GetBaseException().Message}");
            }
        }

        RefreshLibraryMenus();
    }

    private void StopLibraryWatch()
    {
        if (_libraryRefreshTimer is not null)
        {
            _libraryRefreshTimer.Stop();
            _libraryRefreshTimer.Tick -= OnLibraryRefreshTick;
        }

        foreach (var watcher in _libraryWatchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnLibraryChanged;
            watcher.Deleted -= OnLibraryChanged;
            watcher.Changed -= OnLibraryChanged;
            watcher.Renamed -= OnLibraryChanged;
            watcher.Error -= OnLibraryWatcherError;
            watcher.Dispose();
        }

        _libraryWatchers.Clear();
    }

    private void OnLibraryChanged(object sender, FileSystemEventArgs e)
    {
        QueueLibraryRefresh();
    }

    private void OnLibraryWatcherError(object sender, ErrorEventArgs e)
    {
        Trace($"library-watch-error:{e.GetException().GetBaseException().Message}");
        QueueLibraryRefresh();
    }

    private void QueueLibraryRefresh()
    {
        if (_closing)
        {
            return;
        }

        try
        {
            _ = Dispatcher.BeginInvoke(ScheduleLibraryRefresh);
        }
        catch (InvalidOperationException)
        {
            // The window dispatcher is already shutting down.
        }
    }

    private void ScheduleLibraryRefresh()
    {
        if (_closing)
        {
            return;
        }

        _libraryRefreshTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _libraryRefreshTimer.Stop();
        _libraryRefreshTimer.Tick -= OnLibraryRefreshTick;
        _libraryRefreshTimer.Tick += OnLibraryRefreshTick;
        _libraryRefreshTimer.Start();
    }

    private void OnLibraryRefreshTick(object? sender, EventArgs e)
    {
        _libraryRefreshTimer?.Stop();
        if (_closing)
        {
            return;
        }

        // A new file updates the menu only. The loaded model stays put.
        RefreshLibraryMenus();
    }

    private void RefreshLibraryMenus()
    {
        try
        {
            _library = ModelLibraryScanner.Scan(CurrentLibraryRoot());
        }
        catch (Exception exception)
        {
            Trace($"library-scan-failed:{exception.GetBaseException().Message}");
            _library = ModelLibrary.Empty;
        }

        Trace($"library-scan:{_library.Models.Count}:{_library.Motions.Count}:{_library.Music.Count}");
        FillLibraryMenu(_libraryModelsMenu, _library.Models, OnLibraryModelClick);
        FillLibraryMenu(_libraryMotionsMenu, _library.Motions, OnLibraryMotionClick);
        FillLibraryMenu(_libraryMusicMenu, _library.Music, OnLibraryMusicClick);
    }

    private void FillLibraryMenu(MenuItem menu, IReadOnlyList<LibraryFile> files, RoutedEventHandler onClick)
    {
        menu.Items.Clear();
        if (files.Count == 0)
        {
            var empty = new MenuItem
            {
                Header = Text("（空）", "(empty)"),
                IsEnabled = false,
            };
            ConfigureMenuItem(empty);
            menu.Items.Add(empty);
            return;
        }

        foreach (var file in files)
        {
            var item = new MenuItem
            {
                Header = ModelLibraryScanner.EscapeMenuHeader(file.DisplayName),
                Tag = file.FullPath,
                ToolTip = file.FullPath,
                StaysOpenOnClick = true,
            };
            ConfigureMenuItem(item);
            item.Click += onClick;
            menu.Items.Add(item);
        }
    }

    private async Task<bool> TryLoadFirstLibraryModelAsync()
    {
        ModelLibrary library;
        try
        {
            library = ModelLibraryScanner.Scan(CurrentLibraryRoot());
        }
        catch (Exception exception)
        {
            Trace($"library-scan-failed:{exception.GetBaseException().Message}");
            return false;
        }

        if (!StartupModelPolicy.ShouldLoadFirstLibraryModel(
                _settings.LastModelPath,
                IsExistingFile,
                library.Models.Count))
        {
            return false;
        }

        var model = ModelLibraryScanner.SelectDefault(library.Models);
        if (model is null)
        {
            return false;
        }
        Trace($"startup-plan:library-model:{model.FullPath}");
        try
        {
            await LoadModelFileAsync(model.FullPath, recordRecent: true);
        }
        catch (Exception exception) when (IsSuperseded(exception))
        {
            throw;
        }
        catch (Exception exception)
        {
            Trace($"startup-model-skipped:{exception.GetBaseException().Message}");
            return false;
        }

        var motion = ModelLibraryScanner.SelectDefault(library.Motions, preferLoopName: true);
        if (motion is not null)
        {
            Trace($"startup-plan:library-motion:{motion.FullPath}");
            try
            {
                await LoadMotionFileAsync(motion.FullPath, recordRecent: true);
            }
            catch (Exception exception) when (IsSuperseded(exception))
            {
                throw;
            }
            catch (Exception exception)
            {
                Trace($"startup-motion-skipped:{exception.GetBaseException().Message}");
            }
        }

        return true;
    }

    private void OnOpenLibraryFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = CurrentLibraryRoot();
            ModelLibraryScanner.EnsureFolders(root);
            Process.Start(new ProcessStartInfo
            {
                FileName = ModelLibraryScanner.ModelsDirectory(root),
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowActionError(exception);
        }
    }

    private void OnRescanLibraryClick(object sender, RoutedEventArgs e)
    {
        var fresh = UserSettingsStore.Load();
        _settings.LibraryFolder = fresh.LibraryFolder;
        StartLibraryWatch();
    }

    private async void OnLibraryModelClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        await RunUserFileAction(() => LoadModelFileAsync(path));
    }

    private async void OnLibraryMotionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        await RunUserFileAction(() => LoadMotionFileAsync(path));
    }

    private async void OnLibraryMusicClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        await RunUserFileAction(() => LoadMusicFileAsync(path));
    }

    private void RefreshRecentMenus()
    {
        FillRecentMenu(_recentModelsMenu, _settings.RecentModels, OnRecentModelClick);
        FillRecentMenu(_recentMotionsMenu, _settings.RecentMotions, OnRecentMotionClick);
        FillRecentMenu(_recentMusicMenu, _settings.RecentMusic, OnRecentMusicClick);
    }

    private void FillRecentMenu(MenuItem menu, IReadOnlyList<string> paths, RoutedEventHandler onClick)
    {
        menu.Items.Clear();
        var shown = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var item = new MenuItem
            {
                Header = RecentHeader(path),
                Tag = path,
                ToolTip = path,
                StaysOpenOnClick = true,
            };
            ConfigureMenuItem(item);
            item.Click += onClick;
            menu.Items.Add(item);
            shown++;
        }

        menu.Visibility = shown == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string RecentHeader(string path)
    {
        var name = Path.GetFileName(path);
        var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        var header = string.IsNullOrWhiteSpace(parent) ? name : $"{name}  ({parent})";
        return ModelLibraryScanner.EscapeMenuHeader(header);
    }

    private async void OnOpenModelMenuClick(object sender, RoutedEventArgs e)
    {
        var path = PickFile(
            Text("PMX 模型 (*.pmx)|*.pmx", "PMX model (*.pmx)|*.pmx"),
            Text("打开模型", "Open model"),
            _settings.LastModelPath);
        if (path is null)
        {
            return;
        }

        await RunUserFileAction(() => LoadModelFileAsync(path));
    }

    private async void OnOpenMotionMenuClick(object sender, RoutedEventArgs e)
    {
        var path = PickFile(
            Text("VMD 动作 (*.vmd)|*.vmd", "VMD motion (*.vmd)|*.vmd"),
            Text("打开动作", "Open motion"),
            _settings.LastMotionPath);
        if (path is null)
        {
            return;
        }

        await RunUserFileAction(() => LoadMotionFileAsync(path));
    }

    private async void OnOpenMusicMenuClick(object sender, RoutedEventArgs e)
    {
        var filter = "音频 (*.mp3;*.wav;*.ogg;*.m4a;*.aac;*.flac;*.mp4)|*.mp3;*.wav;*.ogg;*.m4a;*.aac;*.flac;*.mp4";
        var path = PickFile(
            filter,
            Text("打开音乐", "Open music"),
            _settings.LastMusicPath);
        if (path is null)
        {
            return;
        }

        await RunUserFileAction(() => LoadMusicFileAsync(path));
    }

    private async void OnRecentModelClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        await RunUserFileAction(() => LoadModelFileAsync(path));
    }

    private async void OnRecentMotionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        await RunUserFileAction(() => LoadMotionFileAsync(path));
    }

    private async void OnRecentMusicClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        await RunUserFileAction(() => LoadMusicFileAsync(path));
    }

    private async void OnCustomMotionMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_customMotionPath is null)
        {
            return;
        }

        await RunUserFileAction(() => LoadMotionFileAsync(_customMotionPath));
    }

    private string? PickFile(string filter, string title, string? initialPath)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
        };
        if (IsExistingFile(initialPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(initialPath);
            dialog.FileName = Path.GetFileName(initialPath!);
        }

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private async Task RunUserFileAction(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (IsSuperseded(exception))
        {
            Trace($"action-superseded:{exception.GetBaseException().Message}");
        }
        catch (Exception exception)
        {
            ShowActionError(exception);
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        e.Handled = true;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        string? model = null;
        string? motion = null;
        string? music = null;
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (model is null && extension.Equals(".pmx", StringComparison.OrdinalIgnoreCase))
            {
                model = file;
            }
            else if (motion is null && extension.Equals(".vmd", StringComparison.OrdinalIgnoreCase))
            {
                motion = file;
            }
            else if (music is null
                && SupportedMusicExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                music = file;
            }
        }

        try
        {
            if (model is not null)
            {
                await LoadModelFileAsync(model);
            }

            if (motion is not null)
            {
                await LoadMotionFileAsync(motion);
            }

            if (music is not null)
            {
                await LoadMusicFileAsync(music);
            }
        }
        catch (Exception exception) when (IsSuperseded(exception))
        {
            Trace($"action-superseded:{exception.GetBaseException().Message}");
        }
        catch (Exception exception)
        {
            ShowActionError(exception);
        }
    }

    private void RememberModel(string path)
    {
        UserSettingsStore.Remember(_settings.RecentModels, path);
        _settings.LastModelPath = Path.GetFullPath(path);
        UserSettingsStore.Save(_settings);
        RefreshRecentMenus();
    }

    private void RememberMotion(string path)
    {
        UserSettingsStore.Remember(_settings.RecentMotions, path);
        _settings.LastMotionPath = Path.GetFullPath(path);
        UserSettingsStore.Save(_settings);
        RefreshRecentMenus();
    }

    private void RememberMusic(string path)
    {
        UserSettingsStore.Remember(_settings.RecentMusic, path);
        _settings.LastMusicPath = Path.GetFullPath(path);
        UserSettingsStore.Save(_settings);
        RefreshRecentMenus();
    }

    private void UpdateSwitchCharacterMenu()
    {
        var bothBundled = File.Exists(_modelPaths[0]) && File.Exists(_modelPaths[1]);
        var show = bothBundled && _modelLoaded && !_usingCustomModel;
        _switchCharacterMenu.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _switchCharacterMenu.IsEnabled = show;
    }

    private void UpdateCustomMotionMenu(string? path)
    {
        if (!IsExistingFile(path))
        {
            _customMotionPath = null;
            _customMotionMenu.Visibility = Visibility.Collapsed;
        }
        else
        {
            _customMotionPath = Path.GetFullPath(path!);
            _customMotionMenu.Header = Path.GetFileNameWithoutExtension(_customMotionPath);
            _customMotionMenu.Tag = _customMotionPath;
            _customMotionMenu.ToolTip = _customMotionPath;
            _customMotionMenu.Visibility = Visibility.Visible;
        }

        var builtInVisible = _builtInDanceMenu.Items.Count > 0;
        _builtInDanceMenu.Visibility = builtInVisible ? Visibility.Visible : Visibility.Collapsed;
        _noDanceMenu.Visibility = !builtInVisible && _customMotionMenu.Visibility != Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateSkinToneMenu(JsonElement message)
    {
        var show = true;
        if (message.TryGetProperty("hasSkinMaterials", out var element)
            && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            show = element.GetBoolean();
        }

        _skinToneMenu.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplySensitiveMenuVisibility()
    {
        var hasSensitive = _modelHasNakedMorph || _modelHasAdultMorphs || _modelHasClothingMorphs;
        _adultContentToggle.Visibility = hasSensitive ? Visibility.Visible : Visibility.Collapsed;
        var show = _adultContentEnabled && hasSensitive;
        _nakedMenu.Visibility = show && _modelHasNakedMorph ? Visibility.Visible : Visibility.Collapsed;
        _clothingMenu.Visibility = show && _modelHasClothingMorphs ? Visibility.Visible : Visibility.Collapsed;
        _adultMenu.Visibility = show && _modelHasAdultMorphs ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnAdultContentToggleClick(object? sender, RoutedEventArgs e)
    {
        _adultContentEnabled = _adultContentToggle.IsChecked == true;
        _settings.AdultContentEnabled = _adultContentEnabled;
        UserSettingsStore.Save(_settings);
        ApplySensitiveMenuVisibility();
        if (_adultContentEnabled || !_modelLoaded)
        {
            return;
        }

        try
        {
            await ClearSensitiveMorphsAsync();
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
        }
    }

    private async Task ClearSensitiveMorphsAsync()
    {
        if (_fullNudeActive && _nakedMenu.Tag is int nakedIndex)
        {
            await SetMorphAsync(nakedIndex, 0);
            _fullNudeActive = false;
        }

        foreach (var submenu in new[] { _clothingMenu, _adultMenu })
        {
            if (!_activeMorphBySubmenu.TryGetValue(submenu, out var index))
            {
                continue;
            }

            await SetMorphAsync(index, 0);
            _activeMorphBySubmenu.Remove(submenu);
        }

        UpdateMorphRadioIndicators();
    }

    private void ShowInfo(string message)
    {
        if (SuppressDialogs)
        {
            Trace($"info-dialog-suppressed:{message}");
            return;
        }

        MessageBox.Show(
            this,
            message,
            "MMD Desk Pet",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowActionError(Exception exception)
    {
        if (SuppressDialogs)
        {
            Trace($"action-dialog-suppressed:{exception.GetBaseException().Message}");
            return;
        }

        if (IsSuperseded(exception)
            || exception.GetBaseException().Message.Contains("already pending", StringComparison.OrdinalIgnoreCase))
        {
            Trace($"action-pending-suppressed:{exception.GetBaseException().Message}");
            return;
        }

        _lastRuntimeError = exception.ToString();
        Trace($"action-failure:{exception}");
        MessageBox.Show(
            this,
            exception.GetBaseException().Message,
            "MMD Desk Pet",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private int CharacterIndexForPath(string path)
    {
        for (var index = 0; index < _modelPaths.Length; index++)
        {
            if (PathsEqual(_modelPaths[index], path))
            {
                return index;
            }
        }

        return 0;
    }

    private bool IsBundledModelPath(string path)
    {
        return _modelPaths.Any(candidate => PathsEqual(candidate, path));
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExistingFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private static string FindRepositoryRoot()
    {
        var startingPoints = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        };

        string? portableRoot = null;
        foreach (var startingPoint in startingPoints)
        {
            if (string.IsNullOrWhiteSpace(startingPoint) || !Directory.Exists(startingPoint))
            {
                continue;
            }

            var directory = new DirectoryInfo(startingPoint);
            for (var depth = 0; depth < 12 && directory != null; depth++, directory = directory.Parent)
            {
                if (IsSourceRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                if (portableRoot is null && IsPortableRoot(directory.FullName))
                {
                    portableRoot = directory.FullName;
                }
            }
        }

        if (portableRoot is not null)
        {
            return portableRoot;
        }

        throw new DirectoryNotFoundException("The MMD Desk Pet application folder could not be located.");
    }

    private static bool IsSourceRoot(string directory)
    {
        return File.Exists(Path.Combine(directory, "web", "index.html"))
            && (File.Exists(Path.Combine(directory, "MMDDeskPet.csproj"))
                || File.Exists(Path.Combine(directory, "HimeMikotoDesktopNative.csproj")));
    }

    private static bool IsPortableRoot(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "web", "index.html")))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, "MMDDeskPet.exe"))
            || File.Exists(Path.Combine(directory, "HimeMikotoDesktopNative.exe"))
            || File.Exists(Path.Combine(directory, "使用说明.txt"));
    }

    private sealed class EventWaiter(string type, TaskCompletionSource<JsonElement> completion)
    {
        public string Type { get; } = type;

        public TaskCompletionSource<JsonElement> Completion { get; } = completion;
    }
}
