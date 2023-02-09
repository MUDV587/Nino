/* this is generated by nino */
namespace Nino.Test
{
    public partial class ComplexData
    {
        public static ComplexData.SerializationHelper NinoSerializationHelper = new ComplexData.SerializationHelper();
        public class SerializationHelper: Nino.Serialization.NinoWrapperBase<ComplexData>
        {
            #region NINO_CODEGEN
            public override void Serialize(ComplexData value, Nino.Serialization.Writer writer)
            {
                if(value == null)
                {
                    writer.Write(false);
                    return;
                }
                writer.Write(true);
                writer.Write(value.a);
                writer.Write(value.b);
                writer.Write(value.c);
                writer.Write(value.d);
                writer.Write(value.e);
                writer.Write(value.f);
                writer.Write(value.g);
                writer.Write(value.h);
                writer.Write(value.i);
                writer.Write(value.j);
            }

            public override ComplexData Deserialize(Nino.Serialization.Reader reader)
            {
                if(!reader.ReadBool())
                    return null;
                ComplexData value = new ComplexData();
                value.a = reader.ReadArray<System.Int32[]>();
                value.b = reader.ReadList<System.Int32[]>();
                value.c = reader.ReadArray<System.Collections.Generic.List<System.Int32>>();
                value.d = reader.ReadDictionary<System.String,System.Collections.Generic.Dictionary<System.String,System.Int32>>();
                value.e = reader.ReadArray<System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.Dictionary<System.String,System.Int32[][]>>>();
                value.f = reader.ReadArray<Nino.Test.Data[]>();
                value.g = reader.ReadList<Nino.Test.Data[]>();
                value.h = reader.ReadArray<Nino.Test.Data[][]>();
                value.i = reader.ReadArray<System.Collections.Generic.List<Nino.Test.Data>>();
                value.j = reader.ReadArray<System.Collections.Generic.List<Nino.Test.Data[]>>();
                return value;
            }
            #endregion
        }
    }
}