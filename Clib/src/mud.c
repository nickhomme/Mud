#ifndef _WIN32
#include <unistd.h>
#endif
#include "../include/mud.h"

#ifdef __clang__
#pragma clang diagnostic push
#pragma ide diagnostic ignored "OCUnusedGlobalDeclarationInspection"
#endif
JavaVMOption* mud_jvm_options(size_t amnt) {
  return malloc(sizeof(JavaVMOption) * amnt);
}

JavaVMOption* mud_jvm_options_va(size_t amnt, ...) {
  JavaVMOption* options = mud_jvm_options(amnt);
  va_list argsList;
  va_start(argsList, amnt);
  for (size_t i = 0; i < amnt; i++) {
    char* arg = va_arg(argsList, char*);
    mud_jvm_options_set(options, i, arg);
  }
  va_end(argsList);
  return options;
}
void mud_jvm_options_set(JavaVMOption* options, size_t index, const char* arg) {
  size_t len = strlen(arg);
//  printf("OptStr: [%zu]`%s`\n", len, arg);
  options[index].optionString = malloc(sizeof(char) * len + 1);
  memcpy(options[index].optionString, arg, sizeof(char) * len);
  options[index].optionString[len] = '\0';
}
JavaVMOption* mud_jvm_options_str_arr(size_t amnt, const char** optionsArr) {
  JavaVMOption* options = mud_jvm_options(amnt);
  for (size_t i = 0; i < amnt; i++) {
    mud_jvm_options_set(options, i, optionsArr[i]);
  }
  return options;
}

Java_JVM_Instance mud_jvm_create_instance(JavaVMOption* options, int amnt) {
//  printf("Creating JVMdd with args: %s\n", args);
//  const size_t argsLen = strlen(args);
//  char* argsCpy = (char*) malloc(sizeof(char) * (argsLen + 1));
//  strcpy(argsCpy, args);
  Java_JVM_Instance instance = {.env = malloc(sizeof(JNIEnv)), .jvm = malloc(sizeof(JavaVM))};
//================== prepare loading of Java VM ============================
  JavaVMInitArgs vm_args;                        // Initialization arguments
  vm_args.options = options;
  vm_args.nOptions = amnt;                          // number of options

  printf("Creating JVM with args:\n");
  for (int i = 0; i < amnt; ++i) {
    printf("\t`%s`\n", options[i].optionString);
  }


//  options[0].optionString = args;   // where to find java .cls
  vm_args.version = JNI_VERSION_1_8;             // minimum Java version

//  vm_args.ignoreUnrecognized = static_cast<jboolean>(false);     // invalid options make the JVM init fail
  vm_args.ignoreUnrecognized = false;
  //=============== load and initialize Java VM and JNI interface =============
  JavaVM** jvm = &instance.jvm;
  JNIEnv** env = &instance.env;
  JavaVMInitArgs* jvmArgs = &vm_args;
  jint rc = JNI_CreateJavaVM(jvm, (void**) env, jvmArgs);  // YES !!
//  options;    // we then no longer need the initialisation options.
  if (rc != JNI_OK) {
    // TO DO: error processing...
//    std::cin.get();
//    if (mud_jvm_check_exception(env, *env));
    exit(EXIT_FAILURE);
  }
  //=============== Display JVM version =======================================
  jint ver = (*instance.env)->GetVersion(instance.env);
  printf("JVM load succeeded: Version %i.%i\n", ((ver >> 16) & 0x0f), (ver & 0x0f));
//  std::cout << "JVM load succeeded: Version ";
//  std::cout << ((ver >> 16) & 0x0f) << "." << (ver & 0x0f) << std::endl;
  return instance;
}

void mud_add_class_path(JNIEnv* env, const char* path) {
//  char urlPath[2048];
//  sprintf(urlPath, "file://%s", path);
//  printf("Adding %s to the classpath\n", path);

  jclass classLoaderCls = mud_get_class(env, "java/lang/ClassLoader");
  jclass urlClassLoaderCls = mud_get_class(env, "java/net/URLClassLoader");
  jclass urlCls = mud_get_class(env, "java/net/URL");

  jmethodID getSystemClassLoaderMethod = (*env)->GetStaticMethodID(env, classLoaderCls, "getSystemClassLoader", "()Ljava/lang/ClassLoader;");
  jobject classLoaderInstance = (*env)->CallStaticObjectMethod(env, classLoaderCls, getSystemClassLoaderMethod);
  jmethodID addUrlMethod = (*env)->GetMethodID(env, urlClassLoaderCls, "addURL", "(Ljava/net/URL;)V");
  jmethodID urlConstructor = (*env)->GetMethodID(env, urlCls, "<init>", "(Ljava/lang/String;)V");
  jstring urlPathStrObj = (*env)->NewStringUTF(env,  path);
  jobject urlInstance = (*env)->NewObject(env, urlCls, urlConstructor, urlPathStrObj);
  (*env)->CallVoidMethod(env, classLoaderInstance, addUrlMethod, urlInstance);
//  mud_string_release(env, urlPathStrObj, urlPath);
//  printf("Added %s to the classpath\n", path);
}

jclass mud_get_class(JNIEnv* env, const char* className) {
  return (*env)->FindClass(env, className);
}

 void mud_release_object(JNIEnv* env, jobject obj) {
  (*env)->DeleteLocalRef(env, obj);
}
jobject mud_new_object(JNIEnv* env, jclass cls, const char* signature, const jvalue * args) {

  jmethodID ctor = (*env)->GetMethodID(env, cls, "<init>", signature);  // FIND AN OBJECT CONSTRUCTOR
  if (!ctor) {
    printf("ERROR: constructor not found matching: %s !\n", signature);
    return NULL;
  }
//  printf("Found ctor: %p\n", ctor);
  if (!args) {
    return (*env)->NewObject(env, cls, ctor);
  }
  jobject obj = (*env)->NewObjectA(env, cls, ctor, args);
  return obj;
}

void mud_jvm_destroy_instance(JavaVM* jvm) {
  (*jvm)->DestroyJavaVM(jvm);
  printf("JVM destroyed\n");
}

jclass mud_get_class_of_obj(JNIEnv* env, jobject obj) {
//  printf("Getting cls for %p\n", obj);
  return (*env)->GetObjectClass(env, obj);
}

void interop_free(ptr pointer) {
  safe_free(pointer);
}
void mud_string_release(JNIEnv* env, jstring message, const char* msgChars) {
  (*env)->ReleaseStringUTFChars(env, message, msgChars);
//  mud_release_object(env, message);
}




jstring mud_string_new(JNIEnv *env, const char* msg) {
//  const size_t strLen = strlen(msg);
//  char* newStr = malloc(sizeof(char) * strLen + 1);
//  memcpy(newStr, msg, sizeof(char) * strLen);
//  newStr[strLen] = '\0';
//  return (struct Java_String_Resp) {
//      .java_ptr = (*env)->NewStringUTF(env, msg),
//      .char_ptr = newStr
//  };
  return (*env)->NewStringUTF(env, msg);
}
jarray mud_array_new(JNIEnv *env, size_t size, jvalue* values, Java_Type type, jclass objCls) {
#define ret_new_arr(type, field) ptr arr = (*env)->New##type##Array(env, size); (*env)->Set##type##ArrayRegion(env, arr, 0, size, (const j##field*) values); return arr;
  if (type == Java_Bool) {
    ret_new_arr(Boolean, boolean)
  } else if (type == Java_Int) {
    ret_new_arr(Int, int)
  } else if (type == Java_Long) {
    ret_new_arr(Long, long)
  } else if (type == Java_Byte) {
    ret_new_arr(Byte, byte)
  } else if (type == Java_Char) {
    ret_new_arr(Char, char)
  } else if (type == Java_Short) {
    ret_new_arr(Short, short)
  } else if (type == Java_Float) {
    ret_new_arr(Float, float)
  } else if (type == Java_Double) {
    ret_new_arr(Double, double)
  }
  jobjectArray arr = (*env)->NewObjectArray(env, size, objCls, null);
  for (jsize i = 0; i < size; ++i) {
    (*env)->SetObjectArrayElement(env, arr, i, values[i].l);
  }
  return arr;
}

jmethodID mud_get_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature) {
  return (*env)->GetMethodID(env, cls,
                             methodName,
                             signature);
}

jmethodID mud_get_static_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature) {

//  printf("Getting static method %s with type signature %s in class %p \n", methodName, signature, cls);

  return (*env)->GetStaticMethodID(env, cls,
                                   methodName,
                                   signature);
}

struct JavaCallResp_S mud_call_static_method(JNIEnv* env, jclass cls, jmethodID method, Java_Type type, const jvalue* args) {
  struct JavaCallResp_S resp = mud_call_handler(env, cls, method, args, type, true);
//  printf("StatucResp: {.ex: _%i_; .vd: _%i_; .val: {.l: _%p_};}\n", resp.is_exception, resp.is_void, resp.value.l);
  return resp;
}

struct JavaCallResp_S mud_call_method(JNIEnv* env, jobject obj, jmethodID method, Java_Type type, const jvalue* args) {
  struct JavaCallResp_S resp = mud_call_handler(env, obj, method, args, type, false);
//  printf("MethodResp[%p]: {.ex: _%i_; .vd: _%i_; .val: {.l: _%p_};}\n", method, resp.is_exception, resp.is_void, resp.value.l);
  return resp;
}
void reprint_str_test(const char* jstr) {
  printf("reprintg: `%s`\n", jstr);
}
char* mud_jstring_to_string(JNIEnv* env, jstring jstr) {

  const char* backingStr = (*env)->GetStringUTFChars(env, jstr, 0);
  const size_t len = strlen(backingStr);
  char* copyStr = malloc(sizeof(char) * (len + 1));
  memcpy(copyStr, backingStr, len);
  copyStr[len] = '\0';
  (*env)->ReleaseStringUTFChars(env, jstr, backingStr);
  return copyStr;
}
jfieldID mud_get_static_field_id(JNIEnv* env, jclass cls, const char* field, const char* signature) {
  jfieldID fieldId = (*env)->GetStaticFieldID(env, cls, field, signature);
//  printf("Getting static field: [%p] `%s`:`%s`:%p\n", cls, field, signature, fieldId);
  return fieldId;
}
jfieldID mud_get_field_id(JNIEnv* env, jclass cls, const char* field, const char* signature) {
  return (*env)->GetFieldID(env, cls, field, signature);
}

jvalue mud_get_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type) {
  return mud_get_field_handler(env, cls, field, type, false);
}
void mud_set_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type, jvalue value) {
  mud_set_field_handler(env, cls, field, type, value, false);
}

jvalue mud_get_static_field_value(JNIEnv* env, jclass cls, jfieldID field, Java_Type type) {
  return mud_get_field_handler(env, cls, field, type, true);
}

void mud_set_static_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type, jvalue value) {
  mud_set_field_handler(env, cls, field, type, value, true);
}

bool mud_instance_of(JNIEnv* env, jobject obj, jclass cls) {
  return (*env)->IsInstanceOf(env, obj, cls);
}

size_t mud_array_length(JNIEnv* env, jarray arr) {
  return (*env)->GetArrayLength(env, arr);
}

jvalue mud_array_get_at(JNIEnv* env, jarray arr, int index, Java_Type type) {
#define retGetMap(name, jtype) jtype val; (*env)->Get##name##ArrayRegion(env, arr, index, 1, &val); return map_value(type, &val);
  if (type == Java_Bool) {
    retGetMap(Boolean, jboolean)
  } else if (type == Java_Int) {
    retGetMap(Int, jint)
  } else if (type == Java_Long) {
    retGetMap(Long, jlong)
  } else if (type == Java_Byte) {
    retGetMap(Byte, jbyte)
  } else if (type == Java_Char) {
    retGetMap(Char, jchar)
  } else if (type == Java_Short) {
    retGetMap(Short, jshort)
  } else if (type == Java_Float) {
    retGetMap(Float, jfloat)
  } else if (type == Java_Double) {
    retGetMap(Double, jdouble)
  } else {
   return (jvalue) {
       .l = (*env)->GetObjectArrayElement(env, arr, index)
   };
  }
}

//jvalue* mud_array_get_all(JNIEnv* env, jarray arr, Java_Type type) {
//  (*env)->GetEle(env, arr, index)
//}

jthrowable mud_jvm_check_exception(JNIEnv* env) {
  if (!(*env)->ExceptionCheck(env)) {
    return null;
  }
  jthrowable ex = (*env)->ExceptionOccurred(env);
//  printf("[Exception]:\n");
//  (*env)->ExceptionDescribe(env);
//  puts("--------");
  (*env)->ExceptionClear(env);
  return ex;
}

#ifdef __clang__
#pragma clang diagnostic push
#pragma ide diagnostic ignored "misc-no-recursion"
#endif
char* mud_get_exception_msg(JNIEnv* env, jthrowable ex, jmethodID getCauseMethod, jmethodID  getStackMethod, jmethodID exToStringMethod, jmethodID frameToStringMethod,
                            bool isTop) {

  char msg[2048];
  memset(msg, 0, 2048);

// Get the array of StackTraceElements.
  jobjectArray frames =
      (jobjectArray) (*env)->CallObjectMethod(env,
                                              ex,
                                              getStackMethod);
  jsize frames_length = (*env)->GetArrayLength(env, frames);

  // Add Throwable.toString() before descending
  // stack trace messages.
  size_t len = strlen(msg);
  if (0 != frames)
  {
    jstring msg_obj =
        (jstring) (*env)->CallObjectMethod(env, ex,
                                           exToStringMethod);
    const char* exMsgStr = (*env)->GetStringUTFChars(env, msg_obj, 0);
    const size_t exMsgStrLen = strlen(exMsgStr);

    // If this is not the top-of-the-trace then
    // this is a cause.
    if (isTop)
    {
      const char* causedByMsg = "Caused by: ";
      const size_t causedByMsgLen = strlen(causedByMsg);
      memcpy(msg + (sizeof(char) * len), causedByMsg, causedByMsgLen);
      len += causedByMsgLen;
    }
    memcpy(msg + (sizeof(char) * len), exMsgStr, exMsgStrLen);

    (*env)->ReleaseStringUTFChars(env, msg_obj, exMsgStr);
    (*env)->DeleteLocalRef(env, msg_obj);
    len += exMsgStrLen;
  }

  // Append stack trace messages if there are any.
//  printf("Frames: %i\n", frames_length);
  if (frames_length > 0)
  {
    jsize i;
    for (i = 0; i < frames_length; i++)
    {
      // Get the string returned from the 'toString()'
      // method of the next frame and append it to
      // the error message.
      jobject frame = (*env)->GetObjectArrayElement(env, frames, i);
      jstring msg_obj =
          (jstring) (*env)->CallObjectMethod(env, frame, frameToStringMethod);

      const char* msg_str = (*env)->GetStringUTFChars(env, msg_obj, 0);
//      printf("msg_str: `%s`\n", msg_str);
      const char* indentMsg = "\n    ";
      const size_t indentMsgLen = 5;
      memcpy(msg + (sizeof(char) * len), indentMsg, indentMsgLen);
      len += indentMsgLen;
      const size_t msgStrLen = strlen(msg_str);
      memcpy(msg + len, msg_str, msgStrLen);
      len += msgStrLen;
//      printf("msg: `%s`_____\n", msg);
      (*env)->ReleaseStringUTFChars(env, msg_obj, msg_str);
      (*env)->DeleteLocalRef(env, msg_obj);
      (*env)->DeleteLocalRef(env, frame);
    }
  }

  // If 'ex' has a cause then append the
  // stack trace messages from the cause.
  if (0 != frames)
  {
    jthrowable cause =
        (jthrowable) (*env)->CallObjectMethod(env,
                                              ex,
                                              getCauseMethod);
    if (0 != cause)
    {
      char* subFrameMsg = mud_get_exception_msg(env,
                                                cause,
                                                getCauseMethod,
                                                getStackMethod,
                                                exToStringMethod,
                                                frameToStringMethod, false);
      const size_t subFrameMsgLen = strlen(subFrameMsg);
      memcpy(msg + (sizeof(char) * len), subFrameMsg, subFrameMsgLen);
      len += subFrameMsgLen;
      free(subFrameMsg);
    }
  }
  char* finalMsg = malloc(sizeof(char) * len + 1);
  memcpy(finalMsg, msg, len);
  finalMsg[len] = '\0';
  return finalMsg;
}
#ifdef __clang__
#pragma clang diagnostic pop
#pragma clang diagnostic pop

#endif