module TelegramIndex.DelayHelper

let delay mean stdDev =
    Seq.initInfinite (fun _ -> CSharpUtils.Rnd.NextGaussian(mean, stdDev) |> abs)
    |> Seq.scan (+) 0. |> Seq.find (fun x -> x > 0.3)
    |> (*) 1000.0 |> int |> Task.delay
