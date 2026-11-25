using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace ArtGoncalense.API;

public static class ProtecaoPiloto
{
    public static void AdicionarProtecaoPiloto(this IServiceCollection services, string? pfxBase64)
    {
        if (string.IsNullOrWhiteSpace(pfxBase64))
        {
            throw new InvalidOperationException("Configure Piloto__CertificadoProtecao.");
        }

        var pfx = Convert.FromBase64String(pfxBase64);
        X509Certificate2 certificado;
        try
        {
            certificado = X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.EphemeralKeySet);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }

        using var chave = certificado.GetRSAPrivateKey();
        if (chave is null || chave.KeySize < 2048)
        {
            certificado.Dispose();
            throw new InvalidOperationException("O certificado de proteção do piloto precisa de chave privada RSA de pelo menos 2048 bits.");
        }

        services.AddSingleton<RepositorioChavesProtecao>();
        services.AddOptions<KeyManagementOptions>().Configure<RepositorioChavesProtecao>(
            (options, repositorio) => options.XmlRepository = repositorio);
        services.AddDataProtection()
            .SetApplicationName("ArtGoncalense.Piloto")
            .ProtectKeysWithCertificate(certificado)
            .UnprotectKeysWithAnyCertificate(certificado);
    }
}
