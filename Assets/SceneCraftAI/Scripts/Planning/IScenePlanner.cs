using System;
using SceneCraftAI.Domain;

namespace SceneCraftAI.Planning
{
    public interface IScenePlanner
    {
        void Plan(string prompt, Action<SceneSpec> onSuccess, Action<string> onError);
    }
}
