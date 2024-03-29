#include "include/mud.h"
//
// Created by Nicholas Homme on 6/19/22.
//
int main(int argc, char **argv) {

  struct JavaCallResp_S respT;
  union jvalue valT;
  printf("%zu\n%zu\n%zu\n%zu\n", sizeof(respT), sizeof(valT), sizeof(bool), sizeof(char));


  JavaVMOption* options = mud_jvm_options_va(1, "-Djava.class.path=../../libs/commons-math3.jar");
  Java_JVM_Instance jvm = mud_jvm_create_instance(options, 1);

  mud_jvm_destroy_instance(jvm.jvm);
  JavaVMOption* options2 = mud_jvm_options_va(1, "-Djava.class.path=../../libs/commons-math3.jar");
  Java_JVM_Instance jvm2 = mud_jvm_create_instance(options2, 1);


  jclass vectorMeanCls = mud_get_class(jvm.env, "org/apache/commons/math3/stat/descriptive/moment/VectorialMean");
  jvalue meanInitArgs = {.i = 2};
  jobject vectorMean = mud_new_object(jvm.env, vectorMeanCls, "(I)V", &meanInitArgs);
  jmethodID vectorResMid = mud_get_method(jvm.env, vectorMeanCls, "getResult", "()[D");
  struct JavaCallResp_S vectorResResp =  mud_call_method(jvm.env, vectorMean, vectorResMid, Java_Object, null);




  jclass  cls = mud_get_class(jvm.env, "org/apache/commons/math3/complex/Complex");
  printf("cls: %p\n", cls);
  jfieldID nanField = mud_get_static_field_id(jvm.env, cls, "NaN", "Lorg/apache/commons/math3/complex/Complex;");
  printf("nan: %p\n", nanField);

  jclass intCls = mud_get_class(jvm.env, "java/lang/Integer");
  jmethodID valueOfMid = mud_get_static_method(jvm.env, intCls, "valueOf", "(I)Ljava/lang/Integer;");
  jvalue intArg = {
      .i = 10
  };
  jobject intObj = mud_new_object(jvm.env, intCls, "(I)V", &intArg);
  jobject int2Obj = mud_call_static_method(jvm.env, intCls, valueOfMid, Java_Object, &intArg).value.l;
  jmethodID intToValueMid = mud_get_method(jvm.env, intCls, "intValue", "()I");
    jvalue args = {
        .i = 9
    };
  struct JavaCallResp_S resp = mud_call_method(jvm.env, intObj, intToValueMid, Java_Int, &args);
  struct JavaCallResp_S resp2 = mud_call_method(jvm.env, int2Obj, intToValueMid, Java_Int, &args);
  printf("Int: {%i} {%i}\n", resp.value.i, resp2.value.i);

//  jobject obj = _java_build_class_object(jvm.env, "MyTest", null);
//  Java_Args* args3 = _java_args_new_ptr(1);
//  Java_Args args = _java_args_new(1);
////  _java_args_add(&args, _java_arg_new_string("Howdy123"));
////  _java_call_method(jvm.env, obj, "echo", _java_type(Java_Void) , args);
////  _java_call_method(jvm.env, obj, "ping", _java_type_object(Java_Object_String), args);
////
//
//
//  _java_args_add(&args, _java_arg_new_float(12));
//  _java_call_method(jvm.env, obj, "Num", _java_type(Java_Int), &args);
//  mud_jvm_destroy_instance(jvm.jvm);
}
