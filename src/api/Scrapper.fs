module TelegramIndex.Scrapper

open System
open System.Text
open FSharp.Control.Tasks.V2.ContextInsensitive
open ScrapperModel
open Telega.Rpc.Dto
open Telega.Rpc.Dto.Types
open TelegramIndex.Utils

let fileLocationToInput (fileLocation: ScrapperModel.FileLocation) =
    let fileInput =
        InputFileLocation.Tag(
            volumeId = fileLocation.VolumeId,
            localId = fileLocation.LocalId,
            secret = fileLocation.Secret,
            fileReference = fileLocation.FileReference.ToBytesUnsafe()
        )
    fileInput |> InputFileLocation.op_Implicit

let scrape (channelPeer: InputPeer.ChannelTag) (state: ScrapperModel.ScrapperState option) (iface: Telegram.Interface) = task {
    let lastMessageId = state |> Option.map (fun x -> x.LastMessageId)
    let inputPeer = InputPeer.op_Implicit channelPeer
    let! batch = Telegram.getChannelHistory inputPeer lastMessageId iface
    let msgs =
        batch.Messages
        |> Seq.rev
        |> Seq.choose (Message.AsTag >> LExt.toOpt)
        |> Seq.sortBy (fun x -> x.Id) // should be sorted after rev
        |> List.ofSeq
    let users =
        batch.Users
        |> Seq.choose (User.AsTag >> LExt.toOpt)
        |> Seq.map (fun x -> (x.Id, x))
        |> Map.ofSeq

    let emptify idx len (s: string) =
        let sb = StringBuilder(s)
        do Seq.init len ((+) idx) |> Seq.iter (fun p -> sb.[p] <- ' ')
        sb.ToString()

    let msgEntities (msg: Message.Tag) =
        msg.Entities |> LExt.toOpt
        |> Option.map List.ofSeq
        |> Option.defaultValue []

    let whoisMark = "#whois"
    let messages =
        msgs
        |> List.filter (fun m -> m.ViaBotId.IsNone && m.FwdFrom.IsNone)
        |> List.map (fun m ->
            m
            |> msgEntities
            |> Seq.choose (fun x -> x.AsHashtagTag() |> LExt.toOpt)
            |> Seq.filter (fun x -> m.Message.Substring(x.Offset, x.Length) = whoisMark)
            |> Seq.filter (fun x -> x.Offset = 0 || x.Offset + x.Length = m.Message.Length)
            |> List.ofSeq
            |> Some
            |> Option.filter (not << List.isEmpty)
            |> Option.map (Seq.fold (fun a x -> a |> emptify x.Offset x.Length) m.Message)
            |> Option.map (fun msg -> msg.Trim())
            |> Option.defaultValue ""
            |> (fun msg -> m.With(message = msg))
        )
        |> List.filter (fun m -> not <| String.IsNullOrWhiteSpace(m.Message))
        |> List.map (fun msg -> (msg, users |> Map.find (msg.FromId |> LExt.toOpt |> Option.get)))
        |> List.filter (fun (rawMsg, rawUser) -> not <| rawUser.Bot)
        |> List.map (fun (rawMsg, rawUser) ->
            let date =
                rawMsg.EditDate |> LExt.toOpt |> Option.defaultValue rawMsg.Date
                |> int64 |> DateTimeOffset.FromUnixTimeSeconds
            let msg = { Id = rawMsg.Id; UserId = rawMsg.FromId |> LExt.toOpt |> Option.get; Text = rawMsg.Message; Date = date }

            let userPhotoLocation =
                rawUser.Photo |> LExt.toOpt |> Option.map (
                    (UserProfilePhoto.AsTag >> LExt.toOpt >> Option.get)
                    >> (fun x -> x.PhotoSmall) >> (FileLocation.AsTag >> LExt.toOpt >> Option.get)
                    >> (fun x -> { VolumeId = x.VolumeId; LocalId = x.LocalId; Secret = x.Secret; FileReference = x.FileReference.ToArrayUnsafe() })
                )
            let user = {
                Id = rawUser.Id
                FirstName = rawUser.FirstName |> LExt.toOpt |> Option.defaultValue null
                LastName = rawUser.LastName |> LExt.toOpt |> Option.defaultValue null
                Username = rawUser.Username |> LExt.toOpt
                PhotoLocation = userPhotoLocation
            }

            (msg, user)
        )

    let optOr opt2 opt1 = match opt1 with Some x -> Some x | None -> opt2
    let newState =
        // if id + batch_limit < total_messages_count then we can start next interation with id + batch_limit
        lastMessageId |> Option.defaultValue 0 |> (+) Telegram.batchLimit |> Some |> Option.filter (fun v -> v < batch.Count)
        // assume that there will be no history segment with batch_limit length in the end of a chat where all messages are deleted
        |> optOr (msgs |> List.tryLast |> Option.map (fun x -> x.Id))
        // if the previous assumption is wrong then the scrapper will stuck on this state
        |> optOr lastMessageId
        // map to state
        |> Option.map (fun x -> { LastMessageId = x })
    return { Messages = messages; State = newState }
}
