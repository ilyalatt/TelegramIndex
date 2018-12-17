module TelegramIndex.Cast

let tryCastAs<'T> (o: obj) =
  match o with
  | :? 'T as res -> Some res
  | _ -> None

let castAs<'T> (o: obj) = o |> tryCastAs<'T> |> Option.get
