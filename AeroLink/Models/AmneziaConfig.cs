using System.Collections.Generic;

namespace AeroLink.Models;

public class AmneziaConfig
{
    public InterfaceConfig Interface { get; set; } = new();
    public List<PeerConfig> Peers { get; set; } = new();
}

public class InterfaceConfig
{
    public string PrivateKey { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DNS { get; set; } = string.Empty;

    //Специфичные Параметры Обфускации WG для тонкой настройки запутывания кода
    public int Jc { get; set; }
    public int Jmin { get; set; }
    public int Jmax { get; set; }
    public int S1 { get; set; }
    public int S2 { get; set; }

    public int H1 { get; set; }
    public int H2 { get; set; }
    public int H3 { get; set; }
    public int H4 { get; set; }
}

public class PeerConfig
{
    public string PublicKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string AllowedIPs { get; set; } = string.Empty;
}