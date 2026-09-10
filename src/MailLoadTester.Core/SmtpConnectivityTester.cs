using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailLoadTester;

public static class SmtpConnectivityTester
{
    public static async Task<string> TestAsync(MailTestOptions o, CancellationToken ct)
    {
        Validation.Validate(o);
        if (o.DryRun)
            return $"DRY-RUN: simulate připojení k {o.SmtpHost}:{o.Port} (Zabezpečení={o.Security}) — žádné reálné spojení.";

        using var client = new SmtpClient
        {
            Timeout = o.ConnectTimeoutMs
        };
        if (o.IgnoreCertificateErrors)
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

        // X509Certificate2 is IDisposable; SmtpClient does not own ClientCertificates entries.
        using var cert = ClientCertificateHelper.Load(o.ClientCertificatePath, o.ClientCertificatePassword);
        if (cert != null)
            client.ClientCertificates.Add(cert);

        string proxyLabel = "Žádná";
        if (!string.IsNullOrWhiteSpace(o.ProxyList))
        {
            var list = ProxyClientFactory.ParseList(o.ProxyList);
            if (list.Count > 0)
            {
                client.ProxyClient = ProxyClientFactory.Create(list[0]);
                proxyLabel = list[0].DisplayKey + (list.Count > 1 ? $" (+{list.Count - 1})" : "");
            }
        }
        else if (o.UseSocks5Proxy && !string.IsNullOrWhiteSpace(o.ProxyHost))
        {
            var ep = new ProxyEndpoint(
                ProxyType.Socks5,
                o.ProxyHost,
                o.ProxyPort,
                string.IsNullOrEmpty(o.ProxyUsername) ? null : o.ProxyUsername,
                string.IsNullOrEmpty(o.ProxyPassword) ? null : o.ProxyPassword);
            client.ProxyClient = ProxyClientFactory.Create(ep);
            proxyLabel = ep.DisplayKey;
        }

        try
        {
            await client.ConnectAsync(o.SmtpHost, o.Port, ToSocketOptions(o.Security), ct).ConfigureAwait(false);
            if (o.UseAuthentication)
            {
                if (o.AuthMethod == SmtpAuthMethod.Auto)
                    await client.AuthenticateAsync(o.Username, o.Password, ct).ConfigureAwait(false);
                else
                {
                    var sasl = AuthMethodHelper.CreateSasl(o.AuthMethod, o.Username, o.Password);
                    await client.AuthenticateAsync(sasl, ct).ConfigureAwait(false);
                }
            }
            return $"OK: {o.SmtpHost}:{o.Port}, {o.Security}, Proxy={proxyLabel}, Auth={(o.UseAuthentication ? "OK" : "off")}";
        }
        finally
        {
            try { if (client.IsConnected) await client.DisconnectAsync(true, ct).ConfigureAwait(false); }
            catch { }
        }
    }

    public static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto
    };
}
