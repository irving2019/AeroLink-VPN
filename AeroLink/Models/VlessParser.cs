using System;
using System.Collections.Generic;
using System.Linq;

namespace AeroLink.Models;

public class VlessParser
{
    public static (string ProfileName, string JsonConfig) Parse(string vlessLink)
    {
        try
        {
            var uri = new Uri(vlessLink.Trim());
            string uuid = uri.UserInfo;
            string host = uri.Host;
            string port = uri.Port.ToString();

            var queryParams = uri.Query.TrimStart('?').Split('&')
                .Select(q => q.Split('='))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].ToLower(), parts => Uri.UnescapeDataString(parts[1]));

            string type = queryParams.GetValueOrDefault("type", "tcp");
            string security = queryParams.GetValueOrDefault("security", "none");
            string pbk = queryParams.GetValueOrDefault("pbk", "");
            string sni = queryParams.GetValueOrDefault("sni", "");
            string fp = queryParams.GetValueOrDefault("fp", "chrome");
            string sid = queryParams.GetValueOrDefault("sid", "");
            string spx = queryParams.GetValueOrDefault("spx", "/");
            string flow = queryParams.GetValueOrDefault("flow", "xtls-rprx-vision");

            string name = string.IsNullOrEmpty(uri.Fragment)
                ? "VLESS Server" : Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));

            string template = @"{
              ""log"": { ""loglevel"": ""warning"" },
              ""inbounds"": [
                {
                  ""tag"": ""tun-in"",
                  ""port"": 10808,
                  ""protocol"": ""tun"",
                  ""settings"": {
                    ""network"": ""172.19.0.1/30"",
                    ""autoRoute"": true,
                    ""strictRoute"": true
                  }
                }
              ],
              ""outbounds"": [
                {
                  ""protocol"": ""vless"",
                  ""settings"": {
                    ""vnext"": [
                      {
                        ""address"": ""{HOST}"",
                        ""port"": {PORT},
                        ""users"": [
                          {
                            ""id"": ""{UUID}"",
                            ""encryption"": ""none"",
                            ""flow"": ""{FLOW}""
                          }
                        ]
                      }
                    ]
                  },
                  ""streamSettings"": {
                    ""network"": ""{TYPE}"",
                    ""security"": ""{SECURITY}"",
                    ""realitySettings"": {
                      ""publicKey"": ""{PBK}"",
                      ""fingerprint"": ""{FP}"",
                      ""serverName"": ""{SNI}"",
                      ""shortId"": ""{SID}"",
                      ""spiderX"": ""{SPX}""
                    }
                  }
                }
              ]
            }";

            string jsonConfig = template
                .Replace("{HOST}", host)
                .Replace("{PORT}", port)
                .Replace("{UUID}", uuid)
                .Replace("{FLOW}", security == "reality" ? flow : "")
                .Replace("{TYPE}", type)
                .Replace("{SECURITY}", security)
                .Replace("{PBK}", pbk)
                .Replace("{FP}", fp)
                .Replace("{SNI}", sni)
                .Replace("{SID}", sid)
                .Replace("{SPX}", spx);

            return (name, jsonConfig);
        }
        catch (Exception ex)
        {
            return ("Ошибка:", $"Ошибка парсинга VLESS: {ex.Message}");
        }
    }
}

