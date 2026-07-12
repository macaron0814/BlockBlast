using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace BlockBlastGame.Editor
{
    [Serializable]
    public class IOSLocalizedInfoPlistEntry
    {
        public string key;
        [TextArea]
        public string value;
    }

    [Serializable]
    public class IOSLocalizationSetting
    {
        public string languageCode = "ja";
        public bool isDefault;
        public IOSLocalizedInfoPlistEntry[] entries;
    }

    [Serializable]
    public class IOSPodDependency
    {
        public string name = "Google-Mobile-Ads-SDK";
        public string version;
    }

    [CreateAssetMenu(fileName = "IOSXcodeBuildSettings", menuName = "BlockBlast/iOS Xcode Build Settings")]
    public class IOSXcodeBuildSettings : ScriptableObject
    {
        [Header("Info.plist Localization")]
        public IOSLocalizationSetting[] localizations =
        {
            new IOSLocalizationSetting
            {
                languageCode = "ja",
                isDefault = true,
                entries = new[]
                {
                    new IOSLocalizedInfoPlistEntry
                    {
                        key = "CFBundleDisplayName",
                        value = "BlockBlast"
                    },
                    new IOSLocalizedInfoPlistEntry
                    {
                        key = "NSUserTrackingUsageDescription",
                        value = "不適切な広告の表示を避けるためにトラッキングの許可を使用します"
                    },
                    new IOSLocalizedInfoPlistEntry
                    {
                        key = "NSLocalNetworkUsageDescription",
                        value = "広告の表示品質を保つために、ネットワーク上の状態を確認します"
                    }
                }
            },
            new IOSLocalizationSetting
            {
                languageCode = "en",
                isDefault = false,
                entries = new[]
                {
                    new IOSLocalizedInfoPlistEntry
                    {
                        key = "CFBundleDisplayName",
                        value = "BlockBlast"
                    },
                    new IOSLocalizedInfoPlistEntry
                    {
                        key = "NSUserTrackingUsageDescription",
                        value = "Allow tracking to help avoid showing inappropriate advertisements."
                    },
                    new IOSLocalizedInfoPlistEntry
                    {
                        key = "NSLocalNetworkUsageDescription",
                        value = "Used to check local network conditions to maintain ad display quality."
                    }
                }
            }
        };

        [Header("Advertising")]
        public bool setGADIsAdManagerApp = false;
        [Tooltip("ON: AdMob iOS banner 用に GADApplicationIdentifier と Google-Mobile-Ads-SDK Pod を追加する。")]
        public bool enableAdMob = true;
        [Tooltip("AdMob App ID。Info.plist の GADApplicationIdentifier に入る。")]
        public string admobApplicationIdentifier = "ca-app-pub-5945355481712765~5781050108";
        public string[] skAdNetworkIdentifiers =
        {
            "cstr6suwn9.skadnetwork"
        };

        [Header("Frameworks")]
        public bool addAppTrackingTransparencyFramework = true;
        public bool addSocialFramework = true;
        public string[] additionalFrameworks =
        {
            "WebKit.framework",
            "UserNotifications.framework",
            "AuthenticationServices.framework"
        };

        [Header("Code Signing")]
        [Tooltip("ON: Xcode project の署名設定を Unity ビルド後に固定する。Cloud Signing を避けたい場合は ON。")]
        public bool configureCodeSigning = true;

        [Tooltip("Apple Developer Team ID。Twinkii Project Inc. は 3WP445845T。")]
        public string developmentTeamId = "3WP445845T";

        [Tooltip("App Store Distribution profile の名前。Apple Developer の profile 名と一致させる。")]
        public string provisioningProfileSpecifier = "Twinkii Project App Distribution";

        [Tooltip("App Store Distribution profile の UUID。空なら specifier のみ使用。")]
        public string provisioningProfileUuid = "fe6d5759-cdd5-435e-868a-64c98b0c176b";

        [Tooltip("Distribution Archive 用の証明書名。通常は Apple Distribution。")]
        public string distributionCodeSignIdentity = "Apple Distribution";

        [Tooltip("ON: Debug構成 (XcodeからのRun実機テスト用) は自動署名(開発用証明書)にする。OFF にすると Debug も Release と同じ Distribution 手動署名になり、Xcodeから直接実機Runできなくなる。")]
        public bool useAutomaticSigningForDebug = true;

        [Header("App Store Icon")]
        [Tooltip("ON: AppIcon.appiconset に App Store 用 1024x1024 (ios-marketing) が無い場合、自動生成する。")]
        public bool ensureMarketingAppIcon = true;

        [Header("CocoaPods / .xcworkspace")]
        [Tooltip("ON: iOS ビルド後に Podfile を生成する。CocoaPods を使う場合、Unity-iPhone.xcworkspace は Podfile から生成される。")]
        public bool generatePodfile = true;

        [Tooltip("ON: Podfile 生成後に pod install を実行し、Unity-iPhone.xcworkspace を生成する。")]
        public bool runPodInstall = true;

        [Tooltip("Podfile の platform :ios に入れる最低 iOS バージョン。")]
        public string podPlatformIOS = "15.0";

        [Tooltip("ON: Podfile に use_frameworks! を追加する。必要な SDK でだけ ON にする。")]
        public bool useFrameworks;

        [Tooltip("UnityFramework target に追加する CocoaPods 依存。Google Mobile Ads などをここで指定する。")]
        public IOSPodDependency[] pods = Array.Empty<IOSPodDependency>();

        [Tooltip("pod install の最大待ち時間 (秒)。0 以下なら待ち時間制限なし。")]
        public int podInstallTimeoutSeconds = 180;
    }

    public static class IOSXcodeBuildPostprocessor
    {
        const string SettingsAssetName = "IOSXcodeBuildSettings";

        [MenuItem("BlockBlast/iOS/Create Xcode Build Settings")]
        public static void CreateSettingsAsset()
        {
            const string directory = "Assets/Editor";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{SettingsAssetName}.asset");
            var settings = ScriptableObject.CreateInstance<IOSXcodeBuildSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            UnityEngine.Debug.Log($"[iOS Xcode Build] Created settings asset: {path}");
        }

        static IOSXcodeBuildSettings LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(IOSXcodeBuildSettings)}");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var settings = AssetDatabase.LoadAssetAtPath<IOSXcodeBuildSettings>(path);
                if (settings != null)
                    return settings;
            }

            return ScriptableObject.CreateInstance<IOSXcodeBuildSettings>();
        }

#if UNITY_IOS
        [PostProcessBuild(1000)]
        public static void ApplyXcodeSettings(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS)
                return;

            var settings = LoadSettings();
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            ApplyInfoPlistLocalization(pathToBuiltProject, projectPath, plist, settings);
            ApplySKAdNetworkItems(plist, settings.skAdNetworkIdentifiers);

            if (settings.setGADIsAdManagerApp)
                plist.root.SetBoolean("GADIsAdManagerApp", true);

            if (settings.enableAdMob && !string.IsNullOrWhiteSpace(settings.admobApplicationIdentifier))
            {
                plist.root.SetString("GADApplicationIdentifier", settings.admobApplicationIdentifier.Trim());
                plist.root.SetBoolean("GADIsAdManagerApp", false);
            }

            RemoveLocalNetworkPromptKeys(plist);

            plist.WriteToFile(plistPath);

            EnsureMarketingAppIcon(pathToBuiltProject, settings);
            ApplyCodeSigning(projectPath, settings);
            ApplyFrameworks(projectPath, settings);
            ApplyDeploymentTarget(projectPath, settings);
            ApplySwiftRuntimeEmbedding(projectPath, settings);
            ApplyCocoaPods(pathToBuiltProject, settings);
            UnityEngine.Debug.Log("[iOS Xcode Build] Applied Xcode project and Info.plist settings from Unity.");
        }

        static void ApplyInfoPlistLocalization(
            string buildPath,
            string projectPath,
            PlistDocument plist,
            IOSXcodeBuildSettings settings)
        {
            var localizations = settings.localizations;
            if (localizations == null || localizations.Length == 0)
                return;

            var localizationArray = plist.root.CreateArray("CFBundleLocalizations");
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string mainTargetGuid = project.GetUnityMainTargetGuid();

            foreach (var localization in localizations)
            {
                if (localization == null || string.IsNullOrWhiteSpace(localization.languageCode))
                    continue;

                localizationArray.AddString(localization.languageCode);
                WriteInfoPlistStrings(buildPath, project, mainTargetGuid, localization);

                if (!localization.isDefault || localization.entries == null)
                    continue;

                foreach (var entry in localization.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        continue;

                    plist.root.SetString(entry.key, entry.value ?? string.Empty);
                }
            }

            project.WriteToFile(projectPath);
            AddKnownRegions(projectPath, localizations);
        }

        static void WriteInfoPlistStrings(
            string buildPath,
            PBXProject project,
            string mainTargetGuid,
            IOSLocalizationSetting localization)
        {
            string lprojDirectoryName = $"{localization.languageCode}.lproj";
            string lprojDirectory = Path.Combine(buildPath, lprojDirectoryName);
            Directory.CreateDirectory(lprojDirectory);

            string fileName = "InfoPlist.strings";
            string filePath = Path.Combine(lprojDirectory, fileName);

            var builder = new StringBuilder();
            if (localization.entries != null)
            {
                foreach (var entry in localization.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        continue;

                    builder.Append(entry.key);
                    builder.Append(" = \"");
                    builder.Append(EscapeInfoPlistString(entry.value ?? string.Empty));
                    builder.AppendLine("\";");
                }
            }

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(false));

            string projectRelativePath = $"{lprojDirectoryName}/{fileName}";
            string fileGuid = project.FindFileGuidByProjectPath(projectRelativePath);
            if (string.IsNullOrEmpty(fileGuid))
                fileGuid = project.AddFile(projectRelativePath, projectRelativePath, PBXSourceTree.Source);

            project.AddFileToBuild(mainTargetGuid, fileGuid);
        }

        static string EscapeInfoPlistString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
        }

        static void AddKnownRegions(string projectPath, IOSLocalizationSetting[] localizations)
        {
            var regionsToAdd = new List<string>();
            foreach (var localization in localizations)
            {
                if (localization == null || string.IsNullOrWhiteSpace(localization.languageCode))
                    continue;

                if (!regionsToAdd.Contains(localization.languageCode))
                    regionsToAdd.Add(localization.languageCode);
            }

            if (regionsToAdd.Count == 0)
                return;

            string projectText = File.ReadAllText(projectPath);
            int knownRegionsIndex = projectText.IndexOf("knownRegions = (", StringComparison.Ordinal);
            if (knownRegionsIndex < 0)
                return;

            int listStartIndex = projectText.IndexOf('\n', knownRegionsIndex);
            int listEndIndex = projectText.IndexOf("\t\t\t);", listStartIndex, StringComparison.Ordinal);
            if (listStartIndex < 0 || listEndIndex < 0)
                return;

            var mergedRegions = new List<string>();
            string existingBlock = projectText.Substring(listStartIndex + 1, listEndIndex - listStartIndex - 1);
            using (var reader = new StringReader(existingBlock))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string region = line.Trim().TrimEnd(',');
                    if (string.IsNullOrEmpty(region) || mergedRegions.Contains(region))
                        continue;

                    mergedRegions.Add(region);
                }
            }

            foreach (string region in regionsToAdd)
            {
                if (!mergedRegions.Contains(region))
                    mergedRegions.Add(region);
            }

            var builder = new StringBuilder();
            builder.Append(projectText, 0, listStartIndex + 1);
            foreach (string region in mergedRegions)
                builder.AppendLine($"\t\t\t\t{region},");
            builder.Append(projectText, listEndIndex, projectText.Length - listEndIndex);

            File.WriteAllText(projectPath, builder.ToString());
        }

        static void ApplySKAdNetworkItems(PlistDocument plist, string[] identifiers)
        {
            if (identifiers == null || identifiers.Length == 0)
                return;

            var array = plist.root.CreateArray("SKAdNetworkItems");
            foreach (string identifier in identifiers)
            {
                if (string.IsNullOrWhiteSpace(identifier))
                    continue;

                PlistElementDict dict = array.AddDict();
                dict.SetString("SKAdNetworkIdentifier", identifier);
            }
        }

        // 注意: NSLocalNetworkUsageDescription / NSBonjourServices を Info.plist から削除しても
        // ローカルネットワークへの確認ダイアログ自体は消えない。このアラートはOSが
        // 「アプリが実際にローカルネットワーク通信 (Bonjour/mDNS/ソケット接続等) を試みた瞬間」に
        // 自動的に出す仕組みで、抑制するAPIは存在しない (Appleの公式仕様、TN3179参照)。
        // キーを削除すると通信自体は止まらず、説明文が空欄の分かりにくいダイアログになるだけで
        // 審査上もマイナスなので、代わりに ApplyInfoPlistLocalization 側で正しい説明文を設定する。
        static void RemoveLocalNetworkPromptKeys(PlistDocument plist)
        {
        }

        static void ApplyFrameworks(string projectPath, IOSXcodeBuildSettings settings)
        {
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            if (settings.addAppTrackingTransparencyFramework)
                project.AddFrameworkToProject(frameworkTargetGuid, "AppTrackingTransparency.framework", true);

            if (settings.addSocialFramework)
                project.AddFrameworkToProject(frameworkTargetGuid, "Social.framework", true);

            if (settings.additionalFrameworks != null)
            {
                foreach (string framework in settings.additionalFrameworks)
                {
                    if (string.IsNullOrWhiteSpace(framework))
                        continue;

                    project.AddFrameworkToProject(frameworkTargetGuid, framework, true);
                }
            }

            project.WriteToFile(projectPath);
        }

        static void ApplyDeploymentTarget(string projectPath, IOSXcodeBuildSettings settings)
        {
            string deploymentTarget = NormalizePodPlatform(settings.podPlatformIOS);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            project.SetBuildProperty(project.GetUnityMainTargetGuid(), "IPHONEOS_DEPLOYMENT_TARGET", deploymentTarget);
            project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "IPHONEOS_DEPLOYMENT_TARGET", deploymentTarget);

            project.WriteToFile(projectPath);
        }

        // GoogleUserMessagingPlatform はSwift製だがUnity側にはSwiftソースが無いため、
        // アプリ本体へSwift標準ライブラリを埋め込むことを明示する。
        // UnityFrameworkはAdMobを静的リンクする側だが、ランタイムを自身へ重複して
        // 埋め込まないよう ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES はNOにする。
        static void ApplySwiftRuntimeEmbedding(string projectPath, IOSXcodeBuildSettings settings)
        {
            if (!settings.enableAdMob)
                return;

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            project.SetBuildProperty(mainTargetGuid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

            project.SetBuildProperty(frameworkTargetGuid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(frameworkTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            project.WriteToFile(projectPath);
        }

        static void ApplyCodeSigning(string projectPath, IOSXcodeBuildSettings settings)
        {
            if (!settings.configureCodeSigning)
                return;

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            string teamId = settings.developmentTeamId ?? string.Empty;
            string identity = string.IsNullOrWhiteSpace(settings.distributionCodeSignIdentity)
                ? "Apple Distribution"
                : settings.distributionCodeSignIdentity.Trim();

            ApplyDistributionSigningToMainTarget(
                project,
                mainTargetGuid,
                teamId,
                identity,
                settings.provisioningProfileSpecifier,
                settings.provisioningProfileUuid,
                settings.useAutomaticSigningForDebug);

            ApplyDistributionSigningToFrameworkTarget(
                project,
                frameworkTargetGuid,
                teamId,
                identity,
                settings.useAutomaticSigningForDebug);

            project.WriteToFile(projectPath);
        }

        // Release構成は申請用の Distribution 証明書 + App Store プロビジョニングプロファイルで手動署名する。
        // Debug構成は (useAutomaticSigningForDebug が ON の場合) 自動署名にしておくことで、
        // Xcode から実機に直接 Run してテストできるようにする。
        // App Store / TestFlight 用の配布プロファイルは Xcode からの直接インストールを許可されていないため、
        // Debug にも Distribution 設定を入れると "Attempted to install a Beta profile without the proper
        // entitlement" (0xe800801f) エラーで実機Runできなくなる。
        static void ApplyDistributionSigningToMainTarget(
            PBXProject project,
            string targetGuid,
            string teamId,
            string identity,
            string profileSpecifier,
            string profileUuid,
            bool useAutomaticSigningForDebug)
        {
            ApplyReleaseDistributionSigning(project, targetGuid, teamId, identity, profileSpecifier, profileUuid);
            ApplyDebugSigning(project, targetGuid, teamId, identity, profileSpecifier, profileUuid, useAutomaticSigningForDebug);
        }

        static void ApplyDistributionSigningToFrameworkTarget(
            PBXProject project,
            string targetGuid,
            string teamId,
            string identity,
            bool useAutomaticSigningForDebug)
        {
            ApplyReleaseDistributionSigning(project, targetGuid, teamId, identity, null, null);
            ApplyDebugSigning(project, targetGuid, teamId, identity, null, null, useAutomaticSigningForDebug);
        }

        static void ApplyReleaseDistributionSigning(
            PBXProject project,
            string targetGuid,
            string teamId,
            string identity,
            string profileSpecifier,
            string profileUuid)
        {
            string releaseConfigGuid = project.BuildConfigByName(targetGuid, "Release");
            if (string.IsNullOrEmpty(releaseConfigGuid))
            {
                // Fallback: フォールバックとして全構成に適用する (Debug/Release の構成名が見つからない場合)。
                project.SetBuildProperty(targetGuid, "CODE_SIGN_STYLE", "Manual");
                project.SetBuildProperty(targetGuid, "CODE_SIGN_IDENTITY", identity);
                project.SetBuildProperty(targetGuid, "DEVELOPMENT_TEAM", teamId);
                if (!string.IsNullOrWhiteSpace(profileSpecifier))
                    project.SetBuildProperty(targetGuid, "PROVISIONING_PROFILE_SPECIFIER", profileSpecifier.Trim());
                if (!string.IsNullOrWhiteSpace(profileUuid))
                    project.SetBuildProperty(targetGuid, "PROVISIONING_PROFILE", profileUuid.Trim());
                return;
            }

            project.SetBuildPropertyForConfig(releaseConfigGuid, "CODE_SIGN_STYLE", "Manual");
            project.SetBuildPropertyForConfig(releaseConfigGuid, "CODE_SIGN_IDENTITY", identity);
            project.SetBuildPropertyForConfig(releaseConfigGuid, "DEVELOPMENT_TEAM", teamId);

            if (!string.IsNullOrWhiteSpace(profileSpecifier))
                project.SetBuildPropertyForConfig(releaseConfigGuid, "PROVISIONING_PROFILE_SPECIFIER", profileSpecifier.Trim());

            if (!string.IsNullOrWhiteSpace(profileUuid))
                project.SetBuildPropertyForConfig(releaseConfigGuid, "PROVISIONING_PROFILE", profileUuid.Trim());
        }

        static void ApplyDebugSigning(
            PBXProject project,
            string targetGuid,
            string teamId,
            string identity,
            string profileSpecifier,
            string profileUuid,
            bool useAutomaticSigningForDebug)
        {
            string debugConfigGuid = project.BuildConfigByName(targetGuid, "Debug");
            if (string.IsNullOrEmpty(debugConfigGuid))
                return;

            if (!useAutomaticSigningForDebug)
            {
                // Debug も Release と同じ Distribution 手動署名にする (Xcodeからの直接実機Runは不可になる)。
                project.SetBuildPropertyForConfig(debugConfigGuid, "CODE_SIGN_STYLE", "Manual");
                project.SetBuildPropertyForConfig(debugConfigGuid, "CODE_SIGN_IDENTITY", identity);
                project.SetBuildPropertyForConfig(debugConfigGuid, "DEVELOPMENT_TEAM", teamId);

                if (!string.IsNullOrWhiteSpace(profileSpecifier))
                    project.SetBuildPropertyForConfig(debugConfigGuid, "PROVISIONING_PROFILE_SPECIFIER", profileSpecifier.Trim());

                if (!string.IsNullOrWhiteSpace(profileUuid))
                    project.SetBuildPropertyForConfig(debugConfigGuid, "PROVISIONING_PROFILE", profileUuid.Trim());

                return;
            }

            project.SetBuildPropertyForConfig(debugConfigGuid, "CODE_SIGN_STYLE", "Automatic");
            project.SetBuildPropertyForConfig(debugConfigGuid, "DEVELOPMENT_TEAM", teamId);
            // 自動署名時はXcodeにDevelopment証明書/プロファイルを選ばせるため、
            // Distribution向けの手動指定を残さない (残っていると自動署名と衝突する)。
            project.SetBuildPropertyForConfig(debugConfigGuid, "CODE_SIGN_IDENTITY", "Apple Development");
            project.SetBuildPropertyForConfig(debugConfigGuid, "PROVISIONING_PROFILE_SPECIFIER", "");
            project.SetBuildPropertyForConfig(debugConfigGuid, "PROVISIONING_PROFILE", "");
        }

        static void EnsureMarketingAppIcon(string buildPath, IOSXcodeBuildSettings settings)
        {
            if (!settings.ensureMarketingAppIcon)
                return;

            string appIconDirectory = Path.Combine(
                buildPath,
                "Unity-iPhone",
                "Images.xcassets",
                "AppIcon.appiconset");
            string contentsPath = Path.Combine(appIconDirectory, "Contents.json");

            if (!Directory.Exists(appIconDirectory) || !File.Exists(contentsPath))
                return;

            string contents = File.ReadAllText(contentsPath);
            if (contents.Contains("\"idiom\" : \"ios-marketing\"") ||
                contents.Contains("\"idiom\":\"ios-marketing\""))
                return;

            string sourceIconPath = FindLargestPng(appIconDirectory);
            if (string.IsNullOrEmpty(sourceIconPath))
                return;

            string marketingIconPath = Path.Combine(appIconDirectory, "Icon-AppStore-1024.png");
            CreateResizedPng(sourceIconPath, marketingIconPath, 1024, 1024);
            AddMarketingIconToContentsJson(contentsPath);
            UnityEngine.Debug.Log($"[iOS Xcode Build] Added missing 1024x1024 App Store icon: {marketingIconPath}");
        }

        static string FindLargestPng(string directory)
        {
            string bestPath = null;
            int bestPixels = 0;

            foreach (string path in Directory.GetFiles(directory, "*.png"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(bytes))
                        continue;

                    int pixels = texture.width * texture.height;
                    if (pixels > bestPixels)
                    {
                        bestPixels = pixels;
                        bestPath = path;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            return bestPath;
        }

        static void CreateResizedPng(string sourcePath, string outputPath, int width, int height)
        {
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(sourceBytes))
                throw new InvalidOperationException($"Failed to load app icon source: {sourcePath}");

            var resized = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = source.GetPixelBilinear(
                        width <= 1 ? 0f : x / (float)(width - 1),
                        height <= 1 ? 0f : y / (float)(height - 1));
                    color.a = 1f;
                    resized.SetPixel(x, y, color);
                }
            }

            resized.Apply();
            File.WriteAllBytes(outputPath, resized.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(resized);
        }

        static void AddMarketingIconToContentsJson(string contentsPath)
        {
            string contents = File.ReadAllText(contentsPath).TrimEnd();
            const string entry =
                "\t\t{\n" +
                "\t\t\t\"filename\" : \"Icon-AppStore-1024.png\",\n" +
                "\t\t\t\"idiom\" : \"ios-marketing\",\n" +
                "\t\t\t\"scale\" : \"1x\",\n" +
                "\t\t\t\"size\" : \"1024x1024\"\n" +
                "\t\t}";

            int imagesEnd = contents.IndexOf("\n\t],", StringComparison.Ordinal);
            if (imagesEnd < 0)
                return;

            string before = contents.Substring(0, imagesEnd);
            string after = contents.Substring(imagesEnd);
            string separator = before.TrimEnd().EndsWith("[", StringComparison.Ordinal) ? "\n" : ",\n";
            File.WriteAllText(contentsPath, before + separator + entry + after + "\n");
        }

        static void ApplyCocoaPods(string buildPath, IOSXcodeBuildSettings settings)
        {
            if (!settings.generatePodfile)
                return;

            WritePodfile(buildPath, settings);

            if (settings.runPodInstall)
                RunPodInstall(buildPath, settings.podInstallTimeoutSeconds);
        }

        static void WritePodfile(string buildPath, IOSXcodeBuildSettings settings)
        {
            string podfilePath = Path.Combine(buildPath, "Podfile");
            var builder = new StringBuilder();

            builder.AppendLine("source 'https://cdn.cocoapods.org/'");
            builder.AppendLine($"platform :ios, '{NormalizePodPlatform(settings.podPlatformIOS)}'");
            builder.AppendLine();
            builder.AppendLine("target 'UnityFramework' do");

            if (settings.useFrameworks)
                builder.AppendLine("  use_frameworks!");

            if (settings.pods != null)
            {
                foreach (var pod in settings.pods)
                {
                    if (pod == null || string.IsNullOrWhiteSpace(pod.name))
                        continue;

                    string podName = EscapeRubySingleQuotedString(pod.name.Trim());
                    if (string.IsNullOrWhiteSpace(pod.version))
                        builder.AppendLine($"  pod '{podName}'");
                    else
                        builder.AppendLine($"  pod '{podName}', '{EscapeRubySingleQuotedString(pod.version.Trim())}'");
                }
            }

            if (settings.enableAdMob)
                builder.AppendLine("  pod 'Google-Mobile-Ads-SDK'");

            builder.AppendLine("end");

            File.WriteAllText(podfilePath, builder.ToString(), new UTF8Encoding(false));
            UnityEngine.Debug.Log($"[iOS Xcode Build] Wrote Podfile: {podfilePath}");
        }

        static string NormalizePodPlatform(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "15.0" : value.Trim();
        }

        static string EscapeRubySingleQuotedString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        static void RunPodInstall(string buildPath, int timeoutSeconds)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-lc \"pod install 2>&1\"",
                WorkingDirectory = buildPath,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                var outputBuilder = new StringBuilder();
                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                        outputBuilder.AppendLine(args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();

                bool exited = timeoutSeconds <= 0
                    ? WaitForProcess(process, -1)
                    : WaitForProcess(process, timeoutSeconds * 1000);

                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        // Best effort only. The build error below is the important signal.
                    }

                    throw new Exception($"pod install timed out after {timeoutSeconds} seconds.");
                }

                process.WaitForExit();
                string output = outputBuilder.ToString();

                if (!string.IsNullOrWhiteSpace(output))
                    UnityEngine.Debug.Log($"[iOS Xcode Build] pod install output:\n{output}");

                if (process.ExitCode != 0)
                    throw new Exception($"pod install failed with exit code {process.ExitCode}:\n{output}");

                string workspacePath = Path.Combine(buildPath, "Unity-iPhone.xcworkspace");
                if (!Directory.Exists(workspacePath))
                    throw new Exception($"pod install completed, but workspace was not found: {workspacePath}");

                UnityEngine.Debug.Log($"[iOS Xcode Build] Generated workspace: {workspacePath}");
            }
        }

        static bool WaitForProcess(Process process, int milliseconds)
        {
            return milliseconds < 0 ? process.WaitForExit(int.MaxValue) : process.WaitForExit(milliseconds);
        }
#endif
    }
}
