using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class ComputerAuthResponse
{
    public string token;
    public ComputerUser user;
}

public sealed class ComputerMeResponse
{
    public ComputerUser user;
}

public sealed class ComputerUser
{
    public int id;
    public string name;
    public string email;
}

public sealed class ComputerGameResponse
{
    public ComputerGameState game;
}

public sealed class ComputerGameState
{
    public string id;
    public string title;
    public int score;
    public bool complete;

    [JsonProperty("created_at")]
    public string createdAt;

    [JsonProperty("agent_mode")]
    public string agentMode;

    [JsonProperty("agent_model")]
    public string agentModel;

    [JsonProperty("agent_error")]
    public string agentError;

    [JsonProperty("last_world_agent_mode")]
    public string lastWorldAgentMode;

    [JsonProperty("world_tick")]
    public int worldTick;

    public Dictionary<string, ComputerValue> values = new Dictionary<string, ComputerValue>();
    public List<ComputerQuest> quests = new List<ComputerQuest>();

    [JsonProperty("quest_log")]
    public List<string> questLog = new List<string>();

    [JsonProperty("generation_log")]
    public List<string> generationLog = new List<string>();

    [JsonProperty("world_feed")]
    public List<string> worldFeed = new List<string>();

    public List<ComputerGoal> goals = new List<ComputerGoal>();

    [JsonProperty("news_items")]
    public List<ComputerNewsItem> newsItems = new List<ComputerNewsItem>();

    public List<ComputerEmailItem> emails = new List<ComputerEmailItem>();

    [JsonProperty("telegram_threads")]
    public List<ComputerTelegramThread> telegramThreads = new List<ComputerTelegramThread>();

    [JsonProperty("action_log")]
    public List<string> actionLog = new List<string>();

    [JsonExtensionData]
    public IDictionary<string, JToken> extraFields;
}

public sealed class ComputerValue
{
    public string label;
    public string description;
    public int value;
}

public sealed class ComputerQuest
{
    public string id;
    public string type;
    public string title;
    public string description;
    public int target;
    public int current;
    public string reward;
    public bool complete;
}

public sealed class ComputerGoal
{
    public string id;
    public string title;
    public int target;
    public int current;
    public bool complete;
}

public sealed class ComputerNewsItem
{
    public string id;
    public string title;
    public string summary;
    public string source;
    public string url;

    [JsonProperty("published_at")]
    public string publishedAt;

    [JsonProperty("truth_label")]
    public string truthLabel;

    [JsonProperty("editor_note")]
    public string editorNote;

    [JsonProperty("public_pressure")]
    public string publicPressure;

    public string decision;
    public bool? correct;
    public int points;

    [JsonProperty("agent_response")]
    public string agentResponse;

    [JsonProperty("agent_reason")]
    public string agentReason;

    [JsonProperty("article_status")]
    public string articleStatus;

    [JsonProperty("article_mode")]
    public string articleMode;

    [JsonProperty("article_byline")]
    public string articleByline;

    [JsonProperty("article_paragraphs")]
    public List<string> articleParagraphs = new List<string>();

    [JsonProperty("article_image_url")]
    public string articleImageUrl;

    [JsonProperty("article_image_caption")]
    public string articleImageCaption;

    [JsonProperty("article_image_credit")]
    public string articleImageCredit;

    [JsonProperty("article_source_url")]
    public string articleSourceUrl;

    [JsonProperty("article_updated_at")]
    public string articleUpdatedAt;

    [JsonProperty("article_error")]
    public string articleError;
}

public sealed class ComputerEmailItem
{
    public string id;

    [JsonProperty("from_name")]
    public string fromName;

    [JsonProperty("from_email")]
    public string fromEmail;

    public string subject;
    public string body;
    public List<JToken> messages = new List<JToken>();
    public List<ComputerOption> options = new List<ComputerOption>();

    [JsonProperty("correct_option")]
    public string correctOption;

    public string selected;

    [JsonProperty("custom_answer")]
    public string customAnswer;

    [JsonProperty("agent_response")]
    public string agentResponse;

    [JsonProperty("agent_reason")]
    public string agentReason;

    [JsonProperty("chat_turns")]
    public int chatTurns;

    [JsonProperty("min_turns")]
    public int minTurns;

    [JsonProperty("max_turns")]
    public int maxTurns;

    public bool resolved;
    public bool? correct;
}

public sealed class ComputerTelegramThread
{
    public string id;
    public string contact;
    public string relationship;
    public List<JToken> messages = new List<JToken>();
    public List<ComputerOption> options = new List<ComputerOption>();

    [JsonProperty("correct_option")]
    public string correctOption;

    public string selected;

    [JsonProperty("custom_answer")]
    public string customAnswer;

    [JsonProperty("agent_response")]
    public string agentResponse;

    [JsonProperty("agent_reason")]
    public string agentReason;

    [JsonProperty("chat_turns")]
    public int chatTurns;

    [JsonProperty("min_turns")]
    public int minTurns;

    [JsonProperty("max_turns")]
    public int maxTurns;

    public bool resolved;
    public bool? correct;
}

public sealed class ComputerOption
{
    public string id;
    public string label;
}
