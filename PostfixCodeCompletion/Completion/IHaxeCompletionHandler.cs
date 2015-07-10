namespace PostfixCodeCompletion
{
    interface IHaxeCompletionHandler
    {
        string GetCompletion(string[] args);
        void Stop();
    }
}
