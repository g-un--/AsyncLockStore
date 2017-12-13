namespace AsyncLockStore

open System
open System.Collections.Generic

type internal StoreMessage = GetToken of string * AsyncReplyChannel<IDisposable> | Continue of string

type public LockStore() = 

    let lockStore = 
        MailboxProcessor<StoreMessage>.Start(fun mbox ->
            let rec handle (lockedKeys : Dictionary<string, List<AsyncReplyChannel<IDisposable>>>) = 
                async {
                    let! message = mbox.Receive()
                
                    match message with
                    | GetToken(key, replyChannel) -> 
                        if lockedKeys.ContainsKey(key) then
                            lockedKeys.[key].Add(replyChannel)
                        else
                            lockedKeys.Add(key, new List<AsyncReplyChannel<IDisposable>>())
                            replyChannel.Reply({ new IDisposable with member x.Dispose() = mbox.Post(Continue(key)) })
                    | Continue(key) -> 
                        let pending = lockedKeys.[key]
                        if pending.Count > 0 then
                            let next = pending.[0]
                            pending.RemoveAt(0)
                            next.Reply({ new IDisposable with member x.Dispose() = mbox.Post(Continue(key)) }) 
                        else
                            lockedKeys.Remove(key) |> ignore
                
                    return! handle lockedKeys
                }
            handle (new Dictionary<string, List<AsyncReplyChannel<IDisposable>>>()))

    member this.GetTokenAsync key = 
        lockStore.PostAndAsyncReply(fun replyChannel -> GetToken(key, replyChannel)) |> Async.StartAsTask
