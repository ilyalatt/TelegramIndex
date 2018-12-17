module TelegramIndex.Api

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Data.HashFunction.xxHash

type Interface = {
    MemStorage: MemStorage.State Var.Var
    PhotoService: PhotoService.Interface
    Log: Log.Interface
}

// what?
let (>>>) route (f: unit -> HttpHandler) =
    route >=> (fun (next: HttpFunc) (ctx: HttpContext) -> f () |> (fun f -> f next ctx))

let xxHash = xxHashFactory.Instance.Create(xxHashConfig(HashSizeInBits = 64))

let computeEtag (loc: ScrapperModel.FileLocation) =
    let bts32 (n: int32) = BitConverter.GetBytes(n)
    let bts64 (n: int64) = BitConverter.GetBytes(n)
    let bts = Array.concat [ bts64 loc.VolumeId; bts32 loc.LocalId; bts64 loc.Secret ]
    xxHash.ComputeHash(bts).AsHexString() |> (fun s -> "\"" + s + "\"")

let api iface =
    let dataVar = iface.MemStorage |> Var.map ApiTransport.mapState |> Var.withMemoization
    choose [
        route "/api/ping" >=> text "pong"
        route "/api/data" >>> fun () -> dataVar |> Var.value |> json

        routef "/api/img/%i" <| fun userId next ctx -> task {
            let notFound () = do ctx.SetStatusCode 404
            let photoLocOpt =
                iface.MemStorage |> Var.value
                |> MemStorage.users
                |> Map.tryFind userId |> Option.bind (fun u -> u.PhotoLocation)
            match photoLocOpt with
            | None -> notFound ()
            | Some photoLoc ->
                let etag = photoLoc |> computeEtag

                let isNotModified =
                    ctx.Request.GetTypedHeaders().IfNoneMatch |> Option.ofObj |> Option.bind Seq.tryHead
                    |> Option.filter (fun ifNoneMatch -> ifNoneMatch.Tag.Value = etag)
                    |> Option.isSome
                if isNotModified then
                    do ctx.SetStatusCode 304
                else
                    let! typeFile = iface.PhotoService |> PhotoService.getOrDownloadAndSave photoLoc
                    let typeStrFile =
                        typeFile
                        |> Option.bind (fun (mimeType, p2, p3) ->
                            match mimeType with
                            | Telegram.FileMimeType.Image x -> Some (x, p2, p3)
                            | _ -> None
                        )
                        |> Option.map (fun (mimeType, p2, p3) -> ("image/" + mimeType.ToString().ToLower(), p2, p3))
                    match typeStrFile with
                    | Some (mimeType, _timestamp, body) ->
                        do ctx.SetContentType(mimeType)
                        do etag |> ctx.SetHttpHeader("etag")
                        do! ctx.Response.Body.WriteAsync(body, 0, body.Length)
                    | None ->
                        do notFound ()
            return Some ctx
        }
    ]

let errorHandler (log: Log.Interface) (ex: Exception) _logger =
    do ignore <| Log.reportException ex log
    clearResponse >=> ServerErrors.INTERNAL_ERROR ()

let configureApp (app: IApplicationBuilder) iface =
    do ignore <| app.UseGiraffeErrorHandler (errorHandler iface.Log)
    do ignore <| app.UseCors(fun b ->
        do ignore <| b.AllowAnyOrigin()
        do ignore <| b.AllowAnyHeader()
        do ignore <| b.AllowAnyMethod()
    )
    do ignore <| app.UseGiraffe (api iface)

let configureServices (services: IServiceCollection) =
    do ignore <| services.AddGiraffe().AddCors()
