using System.Text.Json.Serialization;

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
    public List<Card> Deck { get; } = [];

    public Cards()
    {
        for (var i = 0; i < 2; i++)
        {
            for (var ct = 0; ct <= (int)CardType.DrawTwo; ct++)
            {
                if (i == 1 && ct == (int)CardType.Zero) { continue; }
                for (var cc = 0; cc <= (int)CardColor.Blue; cc++)
                {
                    Deck.Add(new Card((CardType)ct, (CardColor)cc));
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
            (Deck[a], Deck[b]) = (Deck[b], Deck[a]);
        }
    }

    public Card Draw()
    {
        var card = Deck[^1];
        Deck.RemoveAt(Deck.Count - 1);
        return card;
    }
}

record Card(CardType Type, CardColor Color);

[JsonSerializable(typeof(Card))]
partial class CardSerializerContext : JsonSerializerContext { }
