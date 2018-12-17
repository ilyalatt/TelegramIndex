module TelegramIndex.Scrapper

open System
open FSharp.Control.Tasks.V2.ContextInsensitive
open TeleSharp.TL
open ScrapperModel
open Cast

let fileLocationToInput (fileLocation: FileLocation) =
    let fileInput = TLInputFileLocation()
    do fileInput.VolumeId <- fileLocation.VolumeId
    do fileInput.LocalId <- fileLocation.LocalId
    do fileInput.Secret <- fileLocation.Secret
    fileInput

let scrape (channelPeer: TLInputPeerChannel) (state: ScrapperModel.ScrapperState option) (iface: Telegram.Interface) = task {
    let lastMessageId = state |> Option.map (fun x -> x.LastMessageId)
    let! batch = Telegram.getChannelHistory channelPeer lastMessageId iface
    let msgs =
        batch.Messages
        |> Seq.rev
        |> Seq.choose tryCastAs<TLMessage>
        |> Seq.sortBy (fun x -> x.Id) // should be sorted after rev
        |> List.ofSeq
    let users =
        batch.Users
        |> Seq.choose tryCastAs<TLUser>
        |> Seq.map (fun x -> (x.Id, x))
        |> Map.ofSeq

    let messages =
        msgs
        |> List.filter (fun m ->
            m.Message.StartsWith("#whois") &&
            not <| m.ViaBotId.HasValue
        )
        |> List.map (fun msg -> (msg, users |> Map.find msg.FromId.Value))
        |> List.filter (fun (rawMsg, rawUser) -> not <| rawUser.Bot)
        |> List.map (fun (rawMsg, rawUser) ->
            let date =
                rawMsg.EditDate |> Option.ofNullable |> Option.defaultValue rawMsg.Date
                |> int64 |> DateTimeOffset.FromUnixTimeSeconds
            let msg = { Id = rawMsg.Id; UserId = rawMsg.FromId.Value; Text = rawMsg.Message; Date = date }

            let userPhotoLocation =
                rawUser.Photo |> Option.ofObj |> Option.map (
                    castAs<TLUserProfilePhoto>
                    >> (fun x -> x.PhotoSmall) >> castAs<TLFileLocation>
                    >> (fun x -> { VolumeId = x.VolumeId; LocalId = x.LocalId; Secret = x.Secret })
                )
            let user = {
                Id = rawUser.Id
                FirstName = rawUser.FirstName
                LastName = rawUser.LastName
                Username = rawUser.Username |> Option.ofObj
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
