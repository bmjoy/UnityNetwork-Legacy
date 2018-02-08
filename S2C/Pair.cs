namespace MonoLightTech.UnityNetwork.S2C
{
    public sealed class Pair<TLeft, TRight>
    {
        public TLeft Left { get; private set; }
        public TRight Right { get; private set; }

        public Pair(TLeft left, TRight right)
        {
            Left = left;
            Right = right;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(Pair<TLeft, TRight>)) return false;

            Pair<TLeft, TRight> other = (Pair<TLeft, TRight>)obj;
            if (other.Left == null || other.Right == null || Left == null || Right == null) return false;

            return other.Left.Equals(Left) && other.Right.Equals(Right);
        }

        public override int GetHashCode()
        {
            return Left.GetHashCode() ^ Right.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Pair => [Left: {0}] [Right: {1}]", Left, Right);
        }
    }
}