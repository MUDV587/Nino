/* this is generated by nino */
namespace Nino.Test
{
    public partial class CustomTypeTest
    {
        public static CustomTypeTest.SerializationHelper NinoSerializationHelper = new CustomTypeTest.SerializationHelper();
        public class SerializationHelper: Nino.Serialization.NinoWrapperBase<CustomTypeTest>
        {
            #region NINO_CODEGEN
            public override void Serialize(CustomTypeTest value, Nino.Serialization.Writer writer)
            {
                if(value == null)
                {
                    writer.Write(false);
                    return;
                }
                writer.Write(true);
                writer.WriteCommonVal<UnityEngine.Vector3>(value.v3);
                writer.Write(value.dt);
                writer.WriteCommonVal<System.Nullable<System.Int32>>(value.ni);
                writer.Write(value.qs);
                writer.WriteCommonVal<UnityEngine.Matrix4x4>(value.m);
                writer.Write(value.dict);
                writer.Write(value.dict2);
            }

            public override CustomTypeTest Deserialize(Nino.Serialization.Reader reader)
            {
                if(!reader.ReadBool())
                    return null;
                CustomTypeTest value = new CustomTypeTest();
                value.v3 = reader.ReadCommonVal<UnityEngine.Vector3>();
                value.dt = reader.ReadDateTime();
                value.ni = reader.ReadCommonVal<System.Nullable<System.Int32>>();
                value.qs = reader.ReadList<UnityEngine.Quaternion>();
                value.m = reader.ReadCommonVal<UnityEngine.Matrix4x4>();
                value.dict = reader.ReadDictionary<System.String,System.Int32>();
                value.dict2 = reader.ReadDictionary<System.String,Nino.Test.Data>();
                return value;
            }
            #endregion
        }
    }
}