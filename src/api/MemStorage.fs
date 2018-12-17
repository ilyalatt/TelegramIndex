module TelegramIndex.MemStorage

type State = {
    Users: Map<int, ScrapperModel.User>
    Messages: ScrapperModel.Message list
}

let emptyState = { Users = Map.empty; Messages = [] }

let messages state = state.Messages
let users state = state.Users

let private updateUser (u: ScrapperModel.User) state =
    { state with Users = state |> users |> Map.add u.Id u }

let private addMessage (m: ScrapperModel.Message) state =
    { state with Messages = m::state.Messages }

let shouldUpdateUser<'T when 'T : equality> (cmpBy: ScrapperModel.User -> 'T) (user: ScrapperModel.User) (state: State) =
    state |> users |> Map.tryFind user.Id |> Option.map (fun u -> (cmpBy u) = (cmpBy user)) |> Option.defaultValue true

let restoreFromLog (log: LogModel.LogRecord seq) =
    Seq.fold (fun s -> function
        | LogModel.User u -> s |> updateUser u
        | LogModel.Message m -> s |> addMessage m
        | _ -> s
    ) emptyState log

let update (msgs: (ScrapperModel.Message * ScrapperModel.User) list) (state: State) =
    msgs |> Seq.fold (fun s (m, u) ->
        s |> updateUser u |> addMessage m
    ) state
