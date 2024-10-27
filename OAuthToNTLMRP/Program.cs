using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

builder.Services.AddReverseProxy()
    .LoadFromMemory(GetRoutes(), GetClusters())
     .ConfigureHttpClient((context, handler) =>
     {
         // Set credentials here
         handler.Credentials = CredentialCache.DefaultNetworkCredentials;
     })
    .AddTransforms(transforms =>
    {        
        // Load request content fully into buffer instead of providing it as a stream.
        // This is needed to prevent the multi-message NTLM auth flow from
        // prompting the request stream to be read more than once, which is an error.
        transforms.AddRequestTransform(async transformContext => {
            if (transformContext.HttpContext.Request.Method != "GET")
            {
                await transformContext.ProxyRequest.Content.LoadIntoBufferAsync();
            }
            if (transformContext.HttpContext.Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader= transformContext.HttpContext.Request.Headers["Authorization"];

                if (!ValidateAuthHeader(authHeader))
                {
                    transformContext.HttpContext.Response.StatusCode = 401;
                    transformContext.ProxyRequest.Dispose();
                    return;
                }

                transformContext.ProxyRequest.Headers.Remove("Authorization");
            }
            else
            {
                transformContext.HttpContext.Response.StatusCode = 401;
                transformContext.ProxyRequest.Dispose();
                return;

            }
            
        });
    });



builder.Services.AddHttpLogging((o) => { });

//builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpLogging();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

//app.UseAuthentication();


//app.MapReverseProxy().RequireAuthorization();
app.MapReverseProxy().WithHttpLogging(  Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All);

app.Run();


static bool ValidateAuthHeader(string authHeader)
{
    return authHeader.EndsWith("bar");

    var tokenHandler = new JwtSecurityTokenHandler();
    tokenHandler.ValidateToken


}

static IReadOnlyList<RouteConfig> GetRoutes()
{
    return new[]
    {
        new RouteConfig
        {
            RouteId = "secured-route",
            ClusterId = "secured-cluster",
            Match = new RouteMatch
            {
                Path = "/ReportServer/{**catch-all}"
            }
        }
    };
}

static IReadOnlyList<ClusterConfig> GetClusters()
{
    return new[]
    {
        new ClusterConfig
        {
            ClusterId = "secured-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                { "api", new DestinationConfig { Address = "http://localhost/" } }
            }
        }
    };
}