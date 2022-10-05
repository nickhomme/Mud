#include <unistd.h>
#include "../include/mud.h"


JavaVMOption* _java_jvm_options(size_t amnt) {
  return malloc(sizeof(JavaVMOption) * amnt);
}

JavaVMOption* _java_jvm_options_va(size_t amnt, ...) {
  JavaVMOption* options = _java_jvm_options(amnt);
  va_list argsList;
  va_start(argsList, amnt);
  for (size_t i = 0; i < amnt; i++) {
    char* arg = va_arg(argsList, char*);
    size_t len = strlen(arg);
    options[i].optionString = malloc(sizeof(char) * len + 1);
    strcpy(options[i].optionString, arg);
  }
  va_end(argsList);
  return options;
}

JavaVMOption* _java_jvm_options_str_arr(size_t amnt, const char** optionArgs) {
  JavaVMOption* options = _java_jvm_options(amnt);
  for (size_t i = 0; i < amnt; i++) {
    const char* arg = optionArgs[i];
    size_t len = strlen(arg);
    options[i].optionString = malloc(sizeof(char) * len + 1);
    strcpy(options[i].optionString, arg);
  }
  return options;
}

Java_JVM_Instance _java_jvm_create_instance(JavaVMOption* options, int optionsAmnt) {
//  printf("Creating JVMdd with args: %s\n", args);
//  const size_t argsLen = strlen(args);
//  char* argsCpy = (char*) malloc(sizeof(char) * (argsLen + 1));
//  strcpy(argsCpy, args);
  Java_JVM_Instance instance = {.env = malloc(sizeof(JNIEnv)), .jvm = malloc(sizeof(JavaVM))};
//================== prepare loading of Java VM ============================
  JavaVMInitArgs vm_args;                        // Initialization arguments
  vm_args.options = options;
  vm_args.nOptions = optionsAmnt;                          // number of options

  printf("Creating JVM with args:\n");
  for (int i = 0; i < optionsAmnt; ++i) {
    printf("\t%s\n", options[i].optionString);
  }


//  options[0].optionString = args;   // where to find java .cls
  vm_args.version = JNI_VERSION_1_6;             // minimum Java version

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
//    if (_java_jvm_check_exception(env, *env));
    exit(EXIT_FAILURE);
  }
  //=============== Display JVM version =======================================
  jint ver = (*instance.env)->GetVersion(instance.env);
  printf("JVM load succeeded: Version %i.%i\n", ((ver >> 16) & 0x0f), (ver & 0x0f));
//  std::cout << "JVM load succeeded: Version ";
//  std::cout << ((ver >> 16) & 0x0f) << "." << (ver & 0x0f) << std::endl;
  return instance;
}

void _java_add_class_path(JNIEnv* env, const char* path) {
//  char urlPath[2048];
//  sprintf(urlPath, "file://%s", path);
  printf("Adding %s to the classpath\n", path);

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
//  _java_string_release(env, urlPathStrObj, urlPath);
  printf("Added %s to the classpath\n", path);
}

jclass mud_get_class(JNIEnv* env, const char* className) {
  jclass cls = (*env)->FindClass(env, className);
  if (!cls) {
    printf("Error: Class %s not found\n", className);
    exit(1);
  }
//  _java_jvm_check_exception(env);
  return cls;
}

void _java_release_object(JNIEnv* env, jobject obj) {
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

void _java_jvm_destroy_instance(JavaVM* jvm) {
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
void _java_string_release(JNIEnv* env, jstring message, const char* msgChars) {
  (*env)->ReleaseStringUTFChars(env, message, msgChars);
//  _java_release_object(env, message);
}


//Java_Typed_Val _java_call_method_manual(JNIEnv* env,
//                                        jobject obj,
//                                        jclass class,
//                                        const char* methodName,
//                                        const char* methodTyping) {
//  jmethodID method = (*env)->GetMethodID(env, class, methodName, types);
//  jclass urlCls = env->FindClass("java/net/URL");
//  jmethodID urlConstructor = env->GetMethodID(urlCls, "<init>", "(Ljava/lang/String;)V");
//  jobject urlInstance = env->NewObject(urlCls, urlConstructor, env->NewStringUTF(urlPath.c_str()));
//  env->CallVoidMethod(classLoaderInstance, addUrlMethod, urlInstance);
//  std::cout << "Added " << urlPath << " to the classpath." << std::endl;
//}

struct Java_String_Resp mud_string_new(JNIEnv *env, const char* msg) {
  const size_t strLen = strlen(msg);
  char* newStr = malloc(sizeof(char) * strLen + 1);
  memcpy(newStr, msg, sizeof(char) * strLen);
  newStr[strLen] = '\0';
  return (struct Java_String_Resp) {
      .java_ptr = (*env)->NewStringUTF(env, msg),
      .char_ptr = newStr
  };
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

struct JavaCallResp_S mud_call_static_method(JNIEnv* env, jclass cls, jmethodID method, const jvalue* args, Java_Type type) {
  return mud_call_handler(env, cls, method, args, type, true);
}

struct JavaCallResp_S mud_call_method(JNIEnv* env, jobject obj, jmethodID method, const jvalue* args, Java_Type type) {
  return mud_call_handler(env, obj, method, args, type, false);
}

char* mud_jstring_to_string(JNIEnv* env, jstring jstr) {

  const char* backingStr = _java_jstring_to_string(env, jstr);
  const size_t len = strlen(backingStr);
  char* copyStr = malloc(sizeof(char) * (len + 1));
  memcpy(copyStr, backingStr, len);
  copyStr[len] = '\0';
  (*env)->ReleaseStringUTFChars(env, jstr, backingStr);
  return copyStr;
}

jfieldID mud_get_field_id(JNIEnv* env, jclass cls, const char* field, const char* signature) {
  return (*env)->GetFieldID(env, cls, field, signature);
}

jvalue mud_get_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type) {
  return mud_field_handler(env, cls, field, type, false);
}
jvalue mud_get_static_field_value(JNIEnv* env, jclass cls, jfieldID field, Java_Type type) {
  return mud_field_handler(env, cls, field, type, true);
}

bool mud_instance_of(JNIEnv* env, jobject obj, jclass cls) {
  return (*env)->IsInstanceOf(env, obj, cls);
}

size_t mud_array_length(JNIEnv* env, jarray arr) {
  return (*env)->GetArrayLength(env, arr);
}

jvalue mud_array_get_at(JNIEnv* env, jarray arr, int index, Java_Type type) {
#define retGetMap(name, jtype) jtype val; (*env)->Get##name##ArrayRegion(env, arr, index, 1, &val); return map_value(type, &val);
  jboolean a;

  struct JavaCallResp_S result;
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

char* mud_get_exception_msg(JNIEnv* env, jthrowable ex, jmethodID getCauseMethod, jmethodID  getStackMethod, jmethodID exToStringMethod, jmethodID frameToStringMethod,
                            bool isTop) {

   return calloc(1, sizeof(char));

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
      const char* causedByMsg = "\nCaused by: ";
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
  if (frames_length > 0)
  {
    jsize i = 0;
    for (i = 0; i < frames_length; i++)
    {
      // Get the string returned from the 'toString()'
      // method of the next frame and append it to
      // the error message.
      jobject frame = (*env)->GetObjectArrayElement(env, frames, i);
      jstring msg_obj =
          (jstring) (*env)->CallObjectMethod(env, frame, frameToStringMethod);

      const char* msg_str = (*env)->GetStringUTFChars(env, msg_obj, 0);


      const char* indentMsg = "\n    ";
      const size_t indentMsgLen = 5;
      memcpy(msg + (sizeof(char) * len), indentMsg, indentMsgLen);
      len += indentMsgLen;

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