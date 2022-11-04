#ifndef Mud_LIBRARY_H
#define Mud_LIBRARY_H

#include <jni.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>


#ifndef _WIN32
#define EXPORT
#else
#define EXPORT __declspec(dllexport)
#endif

#include "java-arg.h"
#include "../src/memory-util.h"


struct JavaCallResp_S {
  bool is_void;
  bool is_exception;
  jvalue value;
};

EXPORT jthrowable mud_jvm_check_exception(JNIEnv* env);

EXPORT void mud_release_object(JNIEnv* env, jobject obj);

EXPORT void interop_free(ptr pointer);

EXPORT void mud_string_release(JNIEnv* env, jstring message, const char* msgChars);

//static JNIEXPORT void Java_Natives_printf(JNIEnv *env, jobject obj, jstring message) {
//  std::string msg = mud_jstring_to_string(message);
//  printf("%s\n", msg);
//}
typedef struct Java_JVM_Instance_S {
  JavaVM* jvm;
  JNIEnv* env;
} Java_JVM_Instance;

EXPORT JavaVMOption* mud_jvm_options(size_t amnt);
EXPORT void mud_jvm_options_set(JavaVMOption* options, size_t index, const char* val);
EXPORT JavaVMOption* mud_jvm_options_va(size_t amnt, ...);
EXPORT JavaVMOption* mud_jvm_options_str_arr(size_t amnt, const char** options);
EXPORT Java_JVM_Instance mud_jvm_create_instance(JavaVMOption* options, int amnt);
EXPORT void mud_jvm_destroy_instance(JavaVM* jvm);



struct Java_String_Resp {
    jobject java_ptr;
    const char* char_ptr;
};

EXPORT jstring mud_string_new(JNIEnv *env, const char* msg);
EXPORT jarray mud_array_new(JNIEnv *env, size_t size, jvalue* values, Java_Type type, jclass objCls);

//Java_Typed_Val _java_call_method_manual(JNIEnv* env,
//                                 jobject obj,
//                                 const char* methodName,
//                                 const char* retType,
//                                 const char* argsType);


struct Java_Exception_S {
    char* msg;
    char* stack_trace;
};

EXPORT char* mud_get_exception_msg(JNIEnv* env, jthrowable ex, jmethodID getCauseMethod,
                            jmethodID  getStackMethod, jmethodID exToStringMethod,
                            jmethodID frameToStringMethod, bool isTop);

EXPORT void mud_add_class_path(JNIEnv* env, const char* path);
EXPORT jclass mud_get_class(JNIEnv* env, const char* className);
EXPORT jclass mud_get_class_of_obj(JNIEnv* env, jobject obj);

EXPORT jobject mud_new_object(JNIEnv* env, jclass cls, const char* signature, const jvalue * args);

EXPORT jmethodID mud_get_static_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature);
EXPORT jmethodID mud_get_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature);



static jvalue map_value(Java_Type type, ptr val) {
  jvalue value;
  //msvc complains about empty initialize via {}, but the union has random crap in it when not initialized, so we call memset to zero it out to be safe
  memset(&value, 0, sizeof(jvalue));
  if (type == Java_Bool) {
    value.z = *((jbyte*)val);
  } else if (type == Java_Int) {
    value.i = *((jint*)val);
  } else if (type == Java_Long) {
    value.j = *((jlong *)val);
  } else if (type == Java_Byte) {
    value.b = *((jbyte *)val);
  } else if (type == Java_Char) {
    value.c = *((jchar *)val);
  } else if (type == Java_Short) {
    value.s = *((jshort *)val);
  } else if (type == Java_Float) {
    value.f = *((jfloat *)val);
  } else if (type == Java_Double) {
    value.d = *((jdouble*)val);
  } else if (type == Java_Object) {
    value.l = *((jobject*)val);
  }
  return value;
}
static struct JavaCallResp_S map_call_result(JNIEnv* env, Java_Type type, ptr val) {
  struct JavaCallResp_S result = {
      .is_exception = false,
      .is_void = false,
      .value = map_value(type, val),
  };
  if (type == Java_Void) {
    result.is_void = true;
  }
  jthrowable exception = mud_jvm_check_exception(env);
  if (exception) {
    result.value.l = exception;
    result.is_exception = true;
  }
  return result;
}

static struct JavaCallResp_S mud_call_handler(JNIEnv* env, ptr objOrCls, jmethodID method, const jvalue* args, Java_Type type, bool isStatic) {
#define call(name) (isStatic ? (*env)->CallStatic##name##MethodA : (*env)->Call##name##MethodA)(env, objOrCls, method, args)
#define retCallMap(name, jtype) jtype val = call(name); return map_call_result(env, type, &val);
//  printf("Calling %s Method: %p with return of %i on %p\n", isStatic ? "static" : "member", method, type, objOrCls);

  if (type == Java_Bool) {
    retCallMap(Boolean, jboolean)
  } else if (type == Java_Int) {
    retCallMap(Int, jint)
  } else if (type == Java_Long) {
    retCallMap(Long, jlong)
  } else if (type == Java_Byte) {
    retCallMap(Byte, jbyte)
  } else if (type == Java_Char) {
    retCallMap(Char, jchar)
  } else if (type == Java_Short) {
    retCallMap(Short, jshort)
  } else if (type == Java_Float) {
    retCallMap(Float, jfloat)
  } else if (type == Java_Double) {
    retCallMap(Double, jdouble)
  } else if (type == Java_Void) {
    call(Void);
    return map_call_result(env, type, null);
  } else {
    retCallMap(Object, jobject)
  }

}

EXPORT struct JavaCallResp_S mud_call_static_method(JNIEnv* env, jobject obj, jmethodID method, Java_Type type, const jvalue* args);

EXPORT struct JavaCallResp_S mud_call_method(JNIEnv* env, jobject obj, jmethodID method, Java_Type type, const jvalue* args);

static struct JavaCallResp_S mud_call_method_by_name(JNIEnv* env, jclass cls, const char* methodName, const char* signature, Java_Type type, const jvalue* args) {

  return mud_call_method(env, cls, mud_get_method(env, cls, methodName, signature), type, args);
}

static struct JavaCallResp_S mud_call_static_method_by_name(JNIEnv* env, jclass cls, const char* methodName, const char* signature, Java_Type type, const
    jvalue* args) {

  return mud_call_static_method(env, cls, mud_get_static_method(env, cls, methodName, signature), type, args);
}


EXPORT char* mud_jstring_to_string(JNIEnv* env, jstring jstr);
EXPORT void reprint_str_test(const char* jstr);

EXPORT jfieldID mud_get_field_id(JNIEnv* env, jclass cls, const char* field, const char* signature);
EXPORT jfieldID mud_get_static_field_id(JNIEnv* env, jclass cls, const char* field, const char* signature);

static void mud_set_field_handler(JNIEnv* env, ptr objOrCls, jfieldID field, Java_Type type, jvalue value, bool isStatic) {
#define set(name, val) (isStatic ? (*env)->SetStatic##name##Field : (*env)->Set##name##Field)(env, objOrCls, field, val);
  if (type == Java_Bool) {
    set(Boolean, value.z)
  } else if (type == Java_Int) {
    set(Int, value.i)
  } else if (type == Java_Long) {
    set(Long, value.j)
  } else if (type == Java_Byte) {
    set(Byte, value.b)
  } else if (type == Java_Char) {
    set(Char, value.c)
  } else if (type == Java_Short) {
    set(Short, value.s)
  } else if (type == Java_Float) {
    set(Float, value.f)
  } else if (type == Java_Double) {
    set(Double, value.d)
  } else {
    set(Object, value.l)
  }
}

static jvalue jvalue_cast(Java_Type type, ptr val) {
#define return_prop_set(jtype, field) return (jvalue) { .field = *(jtype*) val};

  if (type == Java_Bool) {
    return_prop_set(jboolean, z)
  } else if (type == Java_Int) {
    return_prop_set(jint, i)
  } else if (type == Java_Long) {
    return_prop_set(jlong, j)
  } else if (type == Java_Byte) {
    return_prop_set(jbyte, b)
  } else if (type == Java_Char) {
    return_prop_set(jchar, c)
  } else if (type == Java_Short) {
    return_prop_set(jshort, s)
  } else if (type == Java_Float) {
    return_prop_set(jfloat, f)
  } else if (type == Java_Double) {
    return_prop_set(jdouble, d)
  } else {
    return_prop_set(jobject, l)
  }
}


static jvalue mud_get_field_handler(JNIEnv* env, ptr objOrCls, jfieldID field, Java_Type type, bool isStatic) {
#define get(name) (isStatic ? (*env)->GetStatic##name##Field : (*env)->Get##name##Field)(env, objOrCls, field)

// MSVC does not allow union type casting, so we will explicitly set
#ifdef _WIN32
#define retFieldGetMap(name, jtype) jtype val = get(name); return jvalue_cast(type, &val);
#else
#define retFieldGetMap(name, jtype) return (jvalue) (jtype) get(name);
#endif


  if (type == Java_Bool) {
    
    retFieldGetMap(Boolean, jboolean)
  } else if (type == Java_Int) {
    retFieldGetMap(Int, jint)
  } else if (type == Java_Long) {
    retFieldGetMap(Long, jlong)
  } else if (type == Java_Byte) {
    retFieldGetMap(Byte, jbyte)
  } else if (type == Java_Char) {
    retFieldGetMap(Char, jchar)
  } else if (type == Java_Short) {
    retFieldGetMap(Short, jshort)
  } else if (type == Java_Float) {
    retFieldGetMap(Float, jfloat)
  } else if (type == Java_Double) {
    retFieldGetMap(Double, jdouble)
  } else {
    retFieldGetMap(Object, jobject)
  }

}


EXPORT jvalue mud_get_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type);
EXPORT void mud_set_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type, jvalue value);
EXPORT jvalue mud_get_static_field_value(JNIEnv* env, jclass cls, jfieldID field, Java_Type type);
EXPORT void mud_set_static_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type, jvalue value);
EXPORT bool mud_instance_of(JNIEnv* env, jobject obj, jclass cls);

EXPORT size_t mud_array_length(JNIEnv* env, jarray arr);
EXPORT jvalue mud_array_get_at(JNIEnv* env, jarray arr, int index, Java_Type type);
//jvalue* mud_array_get_all(JNIEnv* env, jarray arr, Java_Type type);

#endif //MUD_LIBRARY_H
