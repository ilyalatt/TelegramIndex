module TelegramIndex.WebApp

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open System


let configureApp iface (app: IApplicationBuilder) =
    do Api.configureApp app iface
    do ignore <| app.UseDefaultFiles().UseStaticFiles()

let configureServices (services: IServiceCollection) =
    do Api.configureServices services
    do ignore <| services

let run iface (args: string[]) =
    let config =
        ConfigurationBuilder()
            .AddCommandLine(args)
            .Build()
    
    let webHostBuilder = WebHostBuilder()
    do ignore <| webHostBuilder
        .UseKestrel()
        .UseConfiguration(config)
        .Configure(Action<IApplicationBuilder> (configureApp iface))
        .ConfigureServices(configureServices)
    webHostBuilder
        .Build()
        .RunAsync()
