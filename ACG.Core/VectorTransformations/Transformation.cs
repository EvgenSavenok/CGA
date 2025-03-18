using System.Numerics;

namespace Graphics.Core.Transformations;

public static class Transformation
{
   public static Matrix4x4 CreateWorldTransform(float scale, Matrix4x4 rotation, Vector3 translation)
   {
      var scaleMatrix = Matrix4x4.CreateScale(scale);
      var translationMatrix = Matrix4x4.CreateTranslation(translation);
      
      var worldMatrix = translationMatrix * rotation * scaleMatrix;
        
      return worldMatrix;
   }

   public static Matrix4x4 CreateViewMatrix(Vector3 eye, Vector3 target, Vector3 up)
   {
      var zAxis = Vector3.Normalize(eye - target); 
      var xAxis = Vector3.Normalize(Vector3.Cross(up, zAxis)); 
      var yAxis =  Vector3.Normalize(Vector3.Cross(zAxis, xAxis)); 
      
      float tx = -Vector3.Dot(xAxis, eye);
      float ty = -Vector3.Dot(yAxis, eye);
      float tz = -Vector3.Dot(zAxis, eye);
      
      var view = new Matrix4x4(
         xAxis.X, xAxis.Y, xAxis.Z, tx,
         yAxis.X, yAxis.Y, yAxis.Z, ty,
         zAxis.X, zAxis.Y, zAxis.Z, tz,
         0.0f,    0.0f,    0.0f,    1.0f);

      view = Matrix4x4.Transpose(view);

      return view;
   }

   public static Matrix4x4 CreatePerspectiveProjection(float fov, float aspect, float znear, float zfar)
   {
      float tanHalfFov = MathF.Tan(fov / 2);
      float m00 = 1 / (aspect * tanHalfFov);
      float m11 = 1 / tanHalfFov;
      float m22 = zfar / (znear - zfar);
      float m32 = (znear * zfar) / (znear - zfar);

      var perspective = new Matrix4x4(
         m00, 0,    0,   0,
         0,   m11,  0,   0,
         0,   0,    m22, m32,
         0,   0,   -1,   0
      );
      
      perspective = Matrix4x4.Transpose(perspective);

      return perspective;
   }
   
   public static Matrix4x4 CreateOrthographicProjection(float width, float height, float zNear, float zFar)
   {
      return Matrix4x4.Transpose(new Matrix4x4(
         2 / width, 0, 0, 0,
         0, 2 / height, 0, 0,
         0, 0, 1 / (zNear - zFar), zNear / (zNear - zFar),
         0, 0, 0, 1
      ));
   }

   public static Matrix4x4 CreateViewportMatrix(float width, float height, float xMin = 0.0f, float yMin = 0.0f)
   {
      var viewportMatrix = new Matrix4x4(
         width / 2,  0,            0,  xMin + width / 2,
         0,         -height / 2,   0,  yMin + height / 2,
         0,          0,            1,  0,
         0,          0,            0,  1
      );

      // Инверсировать из-за конструктора
      viewportMatrix = Matrix4x4.Transpose(viewportMatrix);

      return viewportMatrix;
   }
   
   public static Vector4[] ApplyTransformations(this ObjectModel model,Camera camera, Matrix4x4 finalTransform)
   {
      int count = model.Object.OriginalVertices.Count;
      var vertices = model.TransformedVertices.ToArray();
      Parallel.For(0, count, i =>
      {
         var v = Vector4.Transform(model.TransformedVertices[i], finalTransform);
         if (v.Z > camera.ZNear && v.Z < camera.ZFar) 
         {
            v /= v.W;
         }
         vertices[i] = v;
      });
      return vertices;
   }


   public static Vector4 ApplyTransformations(Vector4 vector, Camera camera, Matrix4x4 finalTransform)
   {

      var v = Vector4.Transform(vector, finalTransform);
      if (v.Z > camera.ZNear && v.Z < camera.ZFar)
      {
         v /= v.W;
      }

      return v;
   }

}