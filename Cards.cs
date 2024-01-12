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

record Card(CardType Type, CardColor Color)
{
    public static string CardTypeToString(CardType type)
    {
        return type switch 
        {
            CardType.Zero => "0",
            CardType.One => "1",
            CardType.Two => "2",
            CardType.Three => "3",
            CardType.Four => "4",
            CardType.Five => "5",
            CardType.Six => "6",
            CardType.Seven => "7",
            CardType.Eight => "8",
            CardType.Nine => "9",
            CardType.Skip => "Aussetzen",
            CardType.Reverse => "Umdrehen",
            CardType.DrawTwo => "Nimm Zwei",
            CardType.Wild => "Wild",
            CardType.WildDrawFour => "Nimm Vier",
            _ => throw new NotImplementedException()
        };
    }

    public static string CardColorToString(CardColor color)
    {
        return color switch 
        {
            CardColor.Red => "Rot",
            CardColor.Yellow => "Gelb",
            CardColor.Green => "Grün",
            CardColor.Blue => "Blau",
            CardColor.Wild => "Wild",
            _ => throw new NotImplementedException()
        };
    }
}

[JsonSerializable(typeof(Card))]
partial class CardSerializerContext : JsonSerializerContext { }
