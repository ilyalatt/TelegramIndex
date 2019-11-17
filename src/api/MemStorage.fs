module TelegramIndex.MemStorage

type State = {
    Users: Map<int, ScrapperModel.User>
    Messages: ScrapperModel.Message list
    Photos: Map<int64, ScrapperModel.PhotoLocation>
}

let emptyState = { Users = Map.empty; Messages = []; Photos = Map.empty }

let messages state = state.Messages
let users state = state.Users
let photos state = state.Photos

let private addUser (u: ScrapperModel.User) state =
    let newUsers = state |> users |> Map.add u.Id u
    let oldPhotos = state |> photos
    let newPhotos =
        u.PhotoLocation
        |> Option.map (fun x -> oldPhotos |> Map.add x.PhotoId x)
        |> Option.defaultValue oldPhotos
    { state with Users = newUsers; Photos = newPhotos }

let private addMessage (m: ScrapperModel.Message) state =
    { state with Messages = m::state.Messages }

let shouldUpdateUser<'T when 'T : equality> (cmpBy: ScrapperModel.User -> 'T) (user: ScrapperModel.User) (state: State) =
    state |> users |> Map.tryFind user.Id |> Option.map (fun u -> (cmpBy u) = (cmpBy user)) |> Option.defaultValue true

let restoreFromLog (log: LogModel.LogRecord seq) =
    Seq.fold (fun s -> function
        | LogModel.User u -> s |> addUser u
        | LogModel.Message m -> s |> addMessage m
        | _ -> s
    ) emptyState log

let update (msgs: (ScrapperModel.Message * ScrapperModel.User) list) (state: State) =
    msgs |> Seq.fold (fun s (m, u) ->
        s |> addUser u |> addMessage m
    ) state
