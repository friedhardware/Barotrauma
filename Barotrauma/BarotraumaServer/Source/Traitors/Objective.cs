﻿using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class Traitor
    {
        public class Objective
        {
            public Traitor Traitor { get; private set; }

            private readonly List<Goal> allGoals = new List<Goal>();
            private readonly List<Goal> pendingGoals = new List<Goal>();
            private readonly List<Goal> completedGoals = new List<Goal>();

            public bool IsCompleted => pendingGoals.Count <= 0;
            public bool IsPartiallyCompleted => completedGoals.Count > 0;
            public bool IsStarted { get; private set; } = false;
            public bool CanBeCompleted => !IsStarted || pendingGoals.All(goal => goal.CanBeCompleted);

            public bool IsEnemy(Character character) => pendingGoals.Any(goal => goal.IsEnemy(character));

            public string InfoText { get; private set; }

            public virtual string GoalInfoFormatId { get; set; } = "TraitorObjectiveGoalInfoFormat";


            public string GoalInfos =>
                string.Join("/",
                    string.Join("/", allGoals.Select((goal, index) =>
                    {
                        var statusText = goal.StatusText;
                        var startIndex = statusText.LastIndexOf('/') + 1;
                        return $"{statusText.Substring(0, startIndex)}[{index}.st]={statusText.Substring(startIndex)}/[{index}.sl]={TextManager.FormatServerMessage(GoalInfoFormatId, new string[] { "[statustext]" }, new string[] { $"[{index}.st]" })}";
                    }).ToArray()),
                    string.Join("", allGoals.Select((goal, index) => $"[{index}.sl]").ToArray()));

            public virtual string StartMessageTextId { get; set; } = "TraitorObjectiveStartMessage";
            public virtual IEnumerable<string> StartMessageKeys => new string[] { "[traitorgoalinfos]" };
            public virtual IEnumerable<string> StartMessageValues => new string[] { GoalInfos };

            public virtual string StartMessageText => TextManager.FormatServerMessageWithGenderPronouns(Traitor.Character.Info.Gender, StartMessageTextId, StartMessageKeys, StartMessageValues);

            public virtual string StartMessageServerTextId { get; set; } = "TraitorObjectiveStartMessageServer";
            public virtual IEnumerable<string> StartMessageServerKeys => StartMessageKeys.Concat(new string[] { "[traitorname]" });
            public virtual IEnumerable<string> StartMessageServerValues => StartMessageValues.Concat(new string[] { Traitor?.Character?.Name ?? "(unknown)" });

            public virtual string StartMessageServerText => TextManager.FormatServerMessageWithGenderPronouns(Traitor.Character.Info.Gender, StartMessageServerTextId, StartMessageServerKeys, StartMessageServerValues);

            public virtual string EndMessageSuccessTextId { get; set; } = "TraitorObjectiveEndMessageSuccess";
            public virtual string EndMessageSuccessDeadTextId { get; set; } = "TraitorObjectiveEndMessageSuccessDead";
            public virtual string EndMessageSuccessDetainedTextId { get; set; } = "TraitorObjectiveEndMessageSuccessDetained";
            public virtual string EndMessageFailureTextId { get; set; } = "TraitorObjectiveEndMessageFailure";
            public virtual string EndMessageFailureDeadTextId { get; set; } = "TraitorObjectiveEndMessageFailureDead";
            public virtual string EndMessageFailureDetainedTextId { get; set; } = "TraitorObjectiveEndMessageFailureDetained";

            public virtual IEnumerable<string> EndMessageKeys => new string[] { "[traitorname]", "[traitorgoalinfos]" };
            public virtual IEnumerable<string> EndMessageValues => new string[] { Traitor?.Character?.Name ?? "(unknown)", GoalInfos };
            public virtual string EndMessageText
            {
                get
                {
                    var traitorIsDead = Traitor.Character.IsDead;
                    var traitorIsDetained = Traitor.Character.LockHands;
                    var messageId = IsCompleted
                        ? (traitorIsDead ? EndMessageSuccessDeadTextId : traitorIsDetained ? EndMessageSuccessDetainedTextId : EndMessageSuccessTextId)
                        : (traitorIsDead ? EndMessageFailureDeadTextId : traitorIsDetained ? EndMessageFailureDetainedTextId : EndMessageFailureTextId);
                    return TextManager.FormatServerMessageWithGenderPronouns(Traitor.Character.Info.Gender, messageId, EndMessageKeys.ToArray(), EndMessageValues.ToArray());
                }
            }

            public bool Start(Traitor traitor)
            {
                Traitor = traitor;
                for (var i = 0; i < pendingGoals.Count;)
                {
                    var goal = pendingGoals[i];
                    if (goal.Start(traitor))
                    {
                        ++i;
                    }
                    else
                    {
                        completedGoals.Add(goal);
                        pendingGoals.RemoveAt(i);
                    }
                }
                if (pendingGoals.Count <= 0)
                {
                    return false;
                }
                IsStarted = true;

                traitor.SendChatMessageBox(StartMessageText);
                traitor.UpdateCurrentObjective(GoalInfos);

                return true;
            }

            public void StartMessage()
            {
                Traitor.SendChatMessage(StartMessageText);
            }

            public void End(bool displayMessage)
            {
                if (displayMessage)
                {
                    Traitor.SendChatMessageBox(EndMessageText);
                }
                // traitor.UpdateCurrentObjective("");
            }

            public void EndMessage()
            {
                Traitor.SendChatMessage(EndMessageText);
            }

            public void Update(float deltaTime)
            {
                if (!IsStarted)
                {
                    return;
                }
                int completedCount = completedGoals.Count;
                for (int i = 0; i < pendingGoals.Count;)
                {
                    var goal = pendingGoals[i];
                    goal.Update(deltaTime);
                    if (!goal.IsCompleted)
                    {
                        ++i;
                    }
                    else
                    {
                        completedGoals.Add(goal);
                        pendingGoals.RemoveAt(i);
                        if (GameMain.Server != null)
                        {
                            Traitor.SendChatMessage(goal.CompletedText);
                            if (pendingGoals.Count > 0)
                            {
                                Traitor.SendChatMessageBox(goal.CompletedText);
                            }
                            Traitor.UpdateCurrentObjective(GoalInfos);
                        }
                    }
                }
            }

            public Objective(string infoText, params Goal[] goals)
            {
                InfoText = infoText;
                allGoals.AddRange(goals);
                pendingGoals.AddRange(goals);
            }
        }
    }
}
