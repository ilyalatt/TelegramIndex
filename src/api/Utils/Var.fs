module TelegramIndex.Var

type Source<'T> (value: 'T) =
    member val version: int = 0 with get, set
    member val value = value with get, set

type DepVar<'T>(computation: unit -> 'T, sourceVersion: unit -> int) =
    member val computation = computation
    member val sourceVersion = sourceVersion

type DepMemoVar<'T>(dep: DepVar<'T>) =
    member val dep = dep
    member val version: int = -1 with get, set
    member val value: 'T = Unchecked.defaultof<'T> with get, set

and Var<'T> = Src of Source<'T> | Dep of DepVar<'T> | DepMemo of DepMemoVar<'T>


let rec value<'T> (var: Var<'T>) : 'T =
    match var with
    | Src v -> v.value
    | Dep v -> v.computation()
    | DepMemo v ->
        if v.version = v.dep.sourceVersion() && v.version <> -1 then
            v.value
        else
            do v.value <- v.dep.computation()
            do v.version <- v.dep.sourceVersion()
            v.value

let create<'T> (value: 'T) : Source<'T> =
    Source(value)

let set<'T> (value: 'T) (source: Source<'T>) : unit =
    do source.value <- value
    do source.version <- source.version + 1

let update<'T> (valueProvider: 'T -> 'T) (source: Source<'T>) : unit =
    do set <| valueProvider source.value <| source

let asVar<'T> (source: Source<'T>) : Var<'T> =
    source |> Src

let withMemoization (var: Var<'T>) : Var<'T> =
    match var with
    | Src v -> Src v
    | Dep v -> DepMemo <| DepMemoVar<'T>(v)
    | DepMemo v -> DepMemo v

let map<'X, 'Y> (mapper: 'X -> 'Y) (var: Var<'X>) : Var<'Y> =
    let sourceVersionGetter =
        match var with
        | Src v -> fun () -> v.version
        | Dep v -> v.sourceVersion
        | DepMemo v -> v.dep.sourceVersion
    let uglyMapper = fun () ->
        var |> value |> mapper
    DepVar<'Y>(uglyMapper, sourceVersionGetter) |> Dep
