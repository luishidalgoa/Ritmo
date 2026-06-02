using System;
using System.Text;

namespace Ritmo.Core.Sync;

/// <summary>
/// Piezas PURAS del login OAuth 2.0 de Microsoft identity platform (v2.0) con PKCE, para
/// sincronizar Microsoft To Do vía Microsoft Graph (#64). A diferencia de Google, las apps de
/// escritorio se registran como CLIENTE PÚBLICO: NO hay secreto de cliente; PKCE + loopback bastan,
/// y <c>offline_access</c> en el scope concede el refresh token. La red/navegador/tokens los hace el host.
/// </summary>
public static class MicrosoftAuth
{
    // Endpoint "common": admite cuentas personales (Outlook/Hotmail) y de trabajo/escuela.
    public const string AuthorizeEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    public const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

    /// <summary>Permisos: leer/escribir tareas de Microsoft To Do + refresh token + perfil básico.</summary>
    public const string TasksScope = "Tasks.ReadWrite offline_access openid profile";

    /// <summary>URL de autorización con PKCE (S256). <c>offline_access</c> (en el scope) da refresh token.</summary>
    public static string AuthorizeUrl(string clientId, string redirectUri, string codeChallenge, string state, string scope = TasksScope)
    {
        var sb = new StringBuilder(AuthorizeEndpoint);
        sb.Append("?response_type=code");
        sb.Append("&client_id=").Append(Uri.EscapeDataString(clientId));
        sb.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        sb.Append("&response_mode=query");
        sb.Append("&code_challenge_method=S256");
        sb.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
        sb.Append("&state=").Append(Uri.EscapeDataString(state));
        sb.Append("&scope=").Append(Uri.EscapeDataString(scope));
        sb.Append("&prompt=select_account");   // deja elegir cuenta (no fuerza re-consentimiento cada vez)
        return sb.ToString();
    }
}
