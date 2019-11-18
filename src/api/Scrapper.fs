module TelegramIndex.Scrapper

open System
open FSharp.Control.Tasks.V2.ContextInsensitive
open ScrapperModel
open Telega.Rpc.Dto.Types
open TelegramIndex.Utils

let fileLocationToInput (loc: ScrapperModel.PhotoLocation) : InputFileLocation =
    let user = loc.User
    let peer: InputPeer =
        InputPeer.UserTag(
            userId = user.Id,
            accessHash = user.AccessHash
        ) |> InputPeer.UserTag.op_Implicit
    InputFileLocation.PeerPhotoTag(
        big = false,
        volumeId = loc.VolumeId,
        localId = loc.LocalId,
        peer = peer
    ) |> InputFileLocation.PeerPhotoTag.op_Implicit

let scrape (channelPeer: InputPeer.ChannelTag) (state: ScrapperModel.ScrapperState option) (iface: Telegram.Interface) = task {
    let lastMessageId = state |> Option.map (fun x -> x.LastMessageId)
    let inputPeer: InputPeer = channelPeer |> InputPeer.ChannelTag.op_Implicit
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

    // * deletes the substring
    // * removes trailing whitespaces in the line around the deleted substring
    // but leaves one whitespace if there are trailing whitespaces left and right to the substring
    // * removes the line if it is empty
    // assumes that substrings are nonoverlapping
    let emptify (s: string) substrings =
        let substrings = substrings |> Seq.sortBy id
        let trySkip n = Seq.indexed >> Seq.skipWhile (fun (i, _x) -> i < n) >> Seq.map snd
        let sb = System.Text.StringBuilder(s)
        let mutable offset = 0
        do substrings |> Seq.iter (fun (pos, len) ->
            let pos = pos + offset
            let leftIdx = pos
            let leftIdx =
                Seq.init leftIdx id
                |> Seq.rev
                |> Seq.takeWhile (fun x -> sb.[x] = ' ')
                |> Seq.tryLast
                |> Option.defaultValue leftIdx
            let rightIdx = pos + len - 1
            let rightIdx =
                Seq.initInfinite ((+) rightIdx)
                |> trySkip 1
                |> Seq.takeWhile (fun x -> x < sb.Length && sb.[x] = ' ')
                |> Seq.tryLast
                |> Option.defaultValue rightIdx
            let isNewLineRight = rightIdx + 1 < sb.Length && sb.[rightIdx + 1] = '\n'
            let isStartLeft = leftIdx = 0
            let isNewLineLeft = not isStartLeft && sb.[leftIdx - 1] = '\n'
            let isSeparatorLeft = isStartLeft || isNewLineLeft
            let isEmptyLine = isNewLineRight && isSeparatorLeft
            let rightIdx = if isEmptyLine then rightIdx + 1 else rightIdx
            let subLen = rightIdx - leftIdx + 1
            let shouldAddWhitespace =
                not isSeparatorLeft && not isNewLineRight && sb.[leftIdx] = ' ' && sb.[rightIdx] = ' '
            do ignore <| sb.Remove(leftIdx, subLen)
            do offset <- offset - subLen
            if shouldAddWhitespace then
                do ignore <| sb.Insert(leftIdx, ' ')
                do offset <- offset + 1
        )
        sb.ToString()
        
    let msgEntities (msg: Message.Tag) =
        msg.Entities |> LExt.toOpt
        |> Option.map List.ofSeq
        |> Option.defaultValue []

    let whoisMark = "#whois"
    let messages =
        msgs
        |> List.filter (fun m -> m.ViaBotId.IsNone && m.FwdFrom.IsNone)
        |> List.filter (fun m -> m.Message.Length >= 20)
        |> List.choose (fun m ->
            m
            |> msgEntities
            |> Seq.choose (fun x -> x.AsHashtagTag() |> LExt.toOpt)
            |> Seq.filter (fun x -> m.Message.Substring(x.Offset, x.Length) = whoisMark)
            |> List.ofSeq
            |> Some
            |> Option.filter (not << List.isEmpty)
            |> Option.map (
                Seq.map (fun x -> (x.Offset, x.Length))
                >> emptify m.Message
                >> fun msgTxt -> msgTxt.Trim()
                >> fun msgTxt -> (m.With(message = msgTxt), users |> Map.find (m.FromId |> LExt.toOpt |> Option.get))
            )
            |> Option.filter (fun (_rawMsg, rawUser) -> not <| rawUser.Bot)
        )
        |> List.map (fun (rawMsg, rawUser) ->
            let date =
                rawMsg.EditDate |> LExt.toOpt |> Option.defaultValue rawMsg.Date
                |> int64 |> DateTimeOffset.FromUnixTimeSeconds
            let msg = { Id = rawMsg.Id; UserId = rawMsg.FromId |> LExt.toOpt |> Option.get; Text = rawMsg.Message; Date = date }

            let userPhotoLocation =
                rawUser.Photo |> LExt.toOpt |> Option.bind (UserProfilePhoto.AsTag >> LExt.toOpt)
                |> Option.bind(fun x ->
                    rawUser.AccessHash |> LExt.toOpt |> Option.map(fun accessHash ->
                        let photo = x.PhotoSmall
                        {
                            VolumeId = photo.VolumeId
                            LocalId = photo.LocalId
                            PhotoId = x.PhotoId
                            User = {
                                Id = rawUser.Id
                                AccessHash = accessHash
                            }
                        }
                    )
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
