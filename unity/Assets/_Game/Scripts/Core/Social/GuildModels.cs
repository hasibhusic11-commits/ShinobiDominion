using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ShinobiDominion.Core
{
    /// <summary>A guild as the server lists it (ADR-042).</summary>
    public sealed class GuildSummary
    {
        public string id;
        public string name;
        public string description;
        public int members;
        public int points;
    }

    public sealed class GuildMember
    {
        public string name;
        public string role;
        public int points;
        public string archetypeId;
        public int level;
        public bool isMe;
    }

    public sealed class GuildChatLine
    {
        public long messageId;
        public string name;
        public string text;
        public long atMs;
        public bool isMe;
    }

    /// <summary>Everything the guild screen shows. It only ever comes from the server.</summary>
    public sealed class GuildView
    {
        public const int MaxMembers = 30;

        public string myName;
        public GuildSummary guild;
        public List<GuildMember> members = new List<GuildMember>();
        public List<GuildChatLine> chat = new List<GuildChatLine>();
        public List<GuildSummary> browse = new List<GuildSummary>();
        public long crystals;
        public long foundingCost;

        public static GuildView Parse(string json)
        {
            var view = ServerState.ParseObject(json).ToObject<GuildView>() ?? throw new FormatException("Server sent an empty guild.");
            if (view.crystals < 0 || view.foundingCost < 0) throw new FormatException("Server sent impossible gems.");
            view.members = view.members ?? new List<GuildMember>();
            view.chat = view.chat ?? new List<GuildChatLine>();
            view.browse = view.browse ?? new List<GuildSummary>();
            return view;
        }
    }
}
