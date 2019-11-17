module TelegramIndex.WebApp

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Https
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open System
open System.Net


let configureApp isProd iface (app: IApplicationBuilder) =
    do Api.configureApp app iface
    if isProd then
        do ignore <| app
            .UseHsts()
            .UseHttpsRedirection()
    do ignore <| app.UseDefaultFiles().UseStaticFiles()

let configureServices (services: IServiceCollection) =
    do Api.configureServices services
    do ignore <| services

let run iface (args: string[]) =
    let envKey = WebHostDefaults.EnvironmentKey
    let config =
        ConfigurationBuilder()
            .AddInMemoryCollection([ Collections.Generic.KeyValuePair<string, string>(envKey, "Development") ])
            .AddCommandLine(args)
            .Build()
    let envVal = config.[envKey]
    let isProd = envVal = "Production"
    
    let webHostBuilder = WebHostBuilder()
    do ignore <| webHostBuilder
        .UseKestrel()
        .ConfigureKestrel(fun o ->
            if isProd then
                do o.Listen(IPAddress.Any, 80)
                do o.Listen(IPAddress.Any, 443, fun o ->
                    do ignore <| o.UseHttps("/root/sns-index/https.pfx")
                    ()
                )
                o.ConfigureHttpsDefaults(fun o ->
                    do o.ClientCertificateMode <- ClientCertificateMode.RequireCertificate
                )
            ()
         )
        .UseConfiguration(config)
        .Configure(Action<IApplicationBuilder> (configureApp isProd iface))
        .ConfigureServices(configureServices)
    webHostBuilder
        .Build()
        .RunAsync()
