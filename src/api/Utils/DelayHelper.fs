module TelegramIndex.DelayHelper

let delayBetween (x: float) (y: float) =
    let inline toMs secs = secs |> (*) 1000.0 |> int
    let random = System.Random()
    random.Next(x |> toMs, y |> toMs) |> Task.delay
