using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<CardType>))]
enum CardType
{
    Zero,
    One,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Skip,
    Reverse,
    DrawTwo,
    Wild,
    WildDrawFour
}

[JsonConverter(typeof(JsonStringEnumConverter<CardColor>))]
enum CardColor
{
    Red,
    Yellow,
    Green,
    Blue,
    Wild
}

class Cards
{
    public Stack<Card> Deck { get; } = [];

    public Cards()
    {
        var deck = new List<Card>();
        for (var i = 0; i < 2; i++)
        {
            for (var ct = 0; ct <= (int)CardType.Nine; ct++)
            {
                if (i == 1 && ct == (int)CardType.Zero) { continue; }
                for (var cc = 0; cc <= (int)CardColor.Blue; cc++)
                {
                    deck.Add(new Card((CardType)ct, (CardColor)cc));
                }
            }
        }

        // TODO: Add support for special cards
        // for (var i = 0; i < 4; i++)
        // {
        //     Deck.Add(new Card(CardType.Wild, CardColor.Wild));
        //     Deck.Add(new Card(CardType.WildDrawFour, CardColor.Wild));
        // }

        // Shuffle
        for (var i = 0; i < 2000; i++)
        {
            var a = Random.Shared.Next(Deck.Count);
            var b = Random.Shared.Next(Deck.Count);
            (deck[a], deck[b]) = (deck[b], deck[a]);
        }

        foreach (var card in deck) { Deck.Push(card); }
    }

    public Card Draw() => Deck.Pop();
}

record Card(CardType Type, CardColor Color);

[JsonSerializable(typeof(Card))]
partial class CardSerializerContext : JsonSerializerContext { }
