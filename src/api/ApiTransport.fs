module TelegramIndex.ApiTransport

open System

type Message = {
    UserId: int
    Date: DateTimeOffset
    Text: string
}

type User = {
    Id: int
    FirstName: string
    LastName: string
    Username: string
    HasPhoto: bool
}

type Data = {
    Messages: Message list
    Users: User list
}

let mapState (state: MemStorage.State) =
    {
        Messages = state |> MemStorage.messages |> Seq.map (fun m ->
        {
            UserId = m.UserId
            Date = m.Date
            Text = m.Text
        }) |> Seq.sortBy (fun x -> x.Date) |> List.ofSeq
        Users = state |> MemStorage.users |> Seq.map (fun kvp -> kvp.Value) |> Seq.map (fun u ->
        {
            Id = u.Id
            FirstName = u.FirstName
            LastName = u.LastName
            Username = u.Username |> Option.defaultValue null
            HasPhoto = u.PhotoLocation |> Option.isSome
        }
        ) |> List.ofSeq
    }
