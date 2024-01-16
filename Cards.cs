using System.Collections.Concurrent;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<CardType>))]
enum CardType
{
    Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine,
    // To keep things simple, we current do not support special cards.
    // Skip,
    // Reverse,
    // DrawTwo,
    // Wild,
    // WildDrawFour
}

[JsonConverter(typeof(JsonStringEnumConverter<CardColor>))]
enum CardColor
{
    Red, Yellow, Green, Blue,
    // Wild
}

class Cards
{
    private ConcurrentStack<Card> Deck { get; } = [];

    public Cards()
    {
        var deck = new Card[10 * 4 + 9 * 4];
        var ix = 0;
        for (var i = 0; i < 2; i++)
        {
            for (var ct = 0; ct <= (int)CardType.Nine; ct++)
            {
                if (i == 1 && ct == (int)CardType.Zero) { continue; }
                for (var cc = 0; cc <= (int)CardColor.Blue; cc++)
                {
                    deck[ix++] = new((CardType)ct, (CardColor)cc);
                }
            }
        }

        // TODO: Add support for special cards

        // Shuffle
        for (var i = 0; i < 20000; i++)
        {
            var a = Random.Shared.Next(Deck.Count);
            var b = Random.Shared.Next(Deck.Count);
            (deck[a], deck[b]) = (deck[b], deck[a]);
        }

        Deck.PushRange(deck);
    }

    public Card Draw() => Deck.TryPop(out var card) ? card : throw new InvalidOperationException("No cards left in deck");

    public bool IsEmpty => Deck.IsEmpty;
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
            // CardType.Skip => "Aussetzen",
            // CardType.Reverse => "Umdrehen",
            // CardType.DrawTwo => "Nimm Zwei",
            // CardType.Wild => "Wild",
            // CardType.WildDrawFour => "Nimm Vier",
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
            //CardColor.Wild => "Wild",
            _ => throw new NotImplementedException()
        };
    }
}

[JsonSerializable(typeof(Card))]
partial class CardSerializerContext : JsonSerializerContext { }
