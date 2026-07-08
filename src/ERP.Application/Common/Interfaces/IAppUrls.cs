namespace ERP.Application.Common.Interfaces;

/// <summary>Public URLs of the front-end, used to build links inside outbound emails.</summary>
public interface IAppUrls
{
    /// <summary>Base URL of the SPA, e.g. <c>http://localhost:5173</c>.</summary>
    string ClientBaseUrl { get; }
}
