using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimDiscordNotifier
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DiscordNotifierPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "io.hexium.valheim.discordnotifier";
        public const string PluginName = "Valheim Discord Notifier";
        public const string PluginVersion = "0.1.0";

        private const int DefaultTimeoutSeconds = 5;
        private const int MinimumTimeoutSeconds = 1;
        private const int MaximumTimeoutSeconds = 30;
        private const int DiscordContentLimit = 2000;

        private static DiscordNotifierPlugin _instance;

        private ConfigEntry<string> _webhookUrl;
        private ConfigEntry<int> _timeoutSeconds;
        private ConfigEntry<string> _readyMessage;
        private ConfigEntry<string> _stoppingMessage;
        private int _readyNotified;
        private int _stoppingNotified;

        private void Awake()
        {
            if (!Application.isBatchMode)
            {
                Logger.LogWarning("This plugin is server-only and is inactive outside a dedicated server process.");
                return;
            }

            _webhookUrl = Config.Bind(
                "Discord",
                "WebhookUrl",
                String.Empty,
                "HTTPS Discord webhook endpoint. It is intentionally blank by default; configure it only in this server's persistent BepInEx config.");
            _timeoutSeconds = Config.Bind(
                "Discord",
                "TimeoutSeconds",
                DefaultTimeoutSeconds,
                "Webhook timeout in seconds. Values outside 1 through 30 use the safe default of 5 seconds.");
            _readyMessage = Config.Bind(
                "Messages",
                "ServerReady",
                "Valheim server is ready.",
                "Discord message sent after the dedicated server finishes generating its world.");
            _stoppingMessage = Config.Bind(
                "Messages",
                "ServerStopping",
                "Valheim server is stopping.",
                "Discord message sent for a normal Valheim shutdown, including a systemd restart.");

            _instance = this;
            try
            {
                new Harmony(PluginGuid).PatchAll(typeof(DiscordNotifierPlugin).Assembly);
            }
            catch (HarmonyException exception)
            {
                Logger.LogError("Lifecycle hooks could not be installed: " + exception.Message);
            }
            catch (TypeLoadException exception)
            {
                Logger.LogError("Lifecycle hooks could not be installed: " + exception.Message);
            }

            Logger.LogInfo("Discord lifecycle notifications are active. Configure Discord.WebhookUrl to enable delivery.");
        }

        private void OnApplicationQuit()
        {
            NotifyServerStopping();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void NotifyServerReady(ZNet network)
        {
            if (!IsDedicatedServer(network) || Interlocked.Exchange(ref _readyNotified, 1) != 0)
            {
                return;
            }

            QueueNotification(_readyMessage.Value, "server-ready");
        }

        private void NotifyServerStopping()
        {
            if (Interlocked.Exchange(ref _stoppingNotified, 1) != 0)
            {
                return;
            }

            SendNotification(_stoppingMessage.Value, "server-stopping");
        }

        private static bool IsDedicatedServer(ZNet network)
        {
            return network != null && network.IsServer() && network.IsDedicated();
        }

        private void QueueNotification(string message, string eventName)
        {
            WebhookRequest request;
            if (!TryCreateRequest(message, eventName, out request))
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate { SendRequest(request); });
        }

        private void SendNotification(string message, string eventName)
        {
            WebhookRequest request;
            if (!TryCreateRequest(message, eventName, out request))
            {
                return;
            }

            SendRequest(request);
        }

        private bool TryCreateRequest(string message, string eventName, out WebhookRequest request)
        {
            request = null;
            Uri endpoint;
            string configuredUrl = _webhookUrl.Value;
            if (String.IsNullOrWhiteSpace(configuredUrl))
            {
                Logger.LogWarning("Discord " + eventName + " notification skipped because Discord.WebhookUrl is empty.");
                return false;
            }

            if (!Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out endpoint) ||
                !String.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                String.IsNullOrEmpty(endpoint.Host) ||
                !String.IsNullOrEmpty(endpoint.UserInfo))
            {
                Logger.LogWarning("Discord " + eventName + " notification skipped because Discord.WebhookUrl is not a valid HTTPS endpoint.");
                return false;
            }

            string content = NormalizeMessage(message);
            if (content.Length == 0)
            {
                Logger.LogWarning("Discord " + eventName + " notification skipped because its message is empty.");
                return false;
            }

            request = new WebhookRequest(endpoint, BuildPayload(content), GetTimeoutMilliseconds(), eventName);
            return true;
        }

        private int GetTimeoutMilliseconds()
        {
            int configuredTimeout = _timeoutSeconds.Value;
            if (configuredTimeout < MinimumTimeoutSeconds || configuredTimeout > MaximumTimeoutSeconds)
            {
                Logger.LogWarning(
                    "Discord.TimeoutSeconds must be between " + MinimumTimeoutSeconds + " and " +
                    MaximumTimeoutSeconds + "; using " + DefaultTimeoutSeconds + " seconds.");
                return DefaultTimeoutSeconds * 1000;
            }

            return configuredTimeout * 1000;
        }

        private void SendRequest(WebhookRequest request)
        {
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(request.Endpoint);
                webRequest.Method = "POST";
                webRequest.ContentType = "application/json";
                webRequest.ContentLength = request.Payload.Length;
                webRequest.Timeout = request.TimeoutMilliseconds;
                webRequest.ReadWriteTimeout = request.TimeoutMilliseconds;

                using (Stream requestStream = webRequest.GetRequestStream())
                {
                    requestStream.Write(request.Payload, 0, request.Payload.Length);
                }

                using (var response = (HttpWebResponse)webRequest.GetResponse())
                {
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                    {
                        Logger.LogWarning("Discord " + request.EventName + " notification was rejected with HTTP " + (int)response.StatusCode + ".");
                        return;
                    }
                }

                Logger.LogInfo("Discord " + request.EventName + " notification sent.");
            }
            catch (WebException exception)
            {
                HttpWebResponse response = exception.Response as HttpWebResponse;
                if (response == null)
                {
                    Logger.LogWarning("Discord " + request.EventName + " notification failed: " + exception.Status + ".");
                    return;
                }

                using (response)
                {
                    Logger.LogWarning("Discord " + request.EventName + " notification was rejected with HTTP " + (int)response.StatusCode + ".");
                }
            }
            catch (IOException)
            {
                Logger.LogWarning("Discord " + request.EventName + " notification failed due to I/O.");
            }
            catch (InvalidOperationException)
            {
                Logger.LogWarning("Discord " + request.EventName + " notification could not be created.");
            }
            catch (NotSupportedException)
            {
                Logger.LogWarning("Discord " + request.EventName + " notification is not supported by this runtime.");
            }
        }

        private static string NormalizeMessage(string message)
        {
            if (String.IsNullOrWhiteSpace(message))
            {
                return String.Empty;
            }

            string trimmed = message.Trim();
            return trimmed.Length <= DiscordContentLimit ? trimmed : trimmed.Substring(0, DiscordContentLimit);
        }

        private static byte[] BuildPayload(string message)
        {
            return new UTF8Encoding(false).GetBytes("{\"content\":\"" + EscapeJson(message) + "\"}");
        }

        private static string EscapeJson(string value)
        {
            var builder = new StringBuilder(value.Length + 16);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        [HarmonyPatch(typeof(ZNet), "OnGenerationFinished")]
        private static class ZNetOnGenerationFinishedPatch
        {
            private static void Postfix(ZNet __instance)
            {
                DiscordNotifierPlugin plugin = _instance;
                if (plugin != null)
                {
                    plugin.NotifyServerReady(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), "Shutdown")]
        private static class ZNetShutdownPatch
        {
            private static void Prefix(ZNet __instance)
            {
                if (IsDedicatedServer(__instance))
                {
                    DiscordNotifierPlugin plugin = _instance;
                    if (plugin != null)
                    {
                        plugin.NotifyServerStopping();
                    }
                }
            }
        }

        private sealed class WebhookRequest
        {
            internal WebhookRequest(Uri endpoint, byte[] payload, int timeoutMilliseconds, string eventName)
            {
                Endpoint = endpoint;
                Payload = payload;
                TimeoutMilliseconds = timeoutMilliseconds;
                EventName = eventName;
            }

            internal Uri Endpoint { get; private set; }
            internal byte[] Payload { get; private set; }
            internal int TimeoutMilliseconds { get; private set; }
            internal string EventName { get; private set; }
        }
    }
}
