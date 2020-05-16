module TelegramIndex.Api

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open FSharp.Control.Tasks.V2.ContextInsensitive

type Interface = {
    MemStorage: MemStorage.State Var.Var
}

// what?
let (>>>) route (f: unit -> HttpHandler) =
    route >=> (fun (next: HttpFunc) (ctx: HttpContext) -> f () |> (fun f -> f next ctx))

let api iface =
    let dataVar = iface.MemStorage |> Var.map ApiTransport.mapState |> Var.withMemoization
    choose [
        route "/api/ping" >=> text "pong"
        route "/api/data" >>> fun () -> dataVar |> Var.value |> json

        routef "/api/img/%d" <| fun photoId _next ctx -> task {
            let setNotFound () = do ctx.SetStatusCode 404
            let photoLocOpt =
                iface.MemStorage |> Var.value
                |> MemStorage.photos
                |> Map.tryFind photoId
            match photoLocOpt with
            | None ->
                do setNotFound ()
            | Some photoLoc ->
                let! photoOpt = PhotoStorage.find photoLoc
                match photoOpt with
                | Some photo ->
                    let body = photo.Body
                    let mimeType = "image/" + photo.Extension.Substring(1)
                    do ctx.SetContentType(mimeType)
                    do ctx.SetHttpHeader "Cache-Control" "max-age=31536000"
                    do! ctx.Response.Body.WriteAsync(body, 0, body.Length)
                | None ->
                    do setNotFound ()
            return Some ctx
        }
    ]

let errorHandler (ex: Exception) _logger =
    do Log.reportException ex
    clearResponse >=> ServerErrors.INTERNAL_ERROR ()

let configureApp (app: IApplicationBuilder) iface =
    do ignore <| app.UseGiraffeErrorHandler errorHandler
    do ignore <| app.UseCors(fun b ->
        do ignore <| b.AllowAnyOrigin()
        do ignore <| b.AllowAnyHeader()
        do ignore <| b.AllowAnyMethod()
    )
    do ignore <| app.UseGiraffe (api iface)

let configureServices (services: IServiceCollection) =
    do ignore <| services.AddGiraffe().AddCors()
