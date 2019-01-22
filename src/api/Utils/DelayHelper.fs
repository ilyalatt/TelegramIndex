module TelegramIndex.DelayHelper

let delayBetween (x: float) (y: float) =
    let inline toMs secs = secs |> (*) 1000.0 |> int
    CSharpUtils.Rnd.NextInt(x |> toMs, y |> toMs) |> Task.delay
