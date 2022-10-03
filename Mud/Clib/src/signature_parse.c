////
//// Created by Nicholas Homme on 10/3/22.
////
//
//#include <stddef.h>
//#include <stdlib.h>
//#include <memory.h>
//#include "signature_parse.h"
//
//
//typedef struct Scanner_S {
//  const char* src;
//  size_t index;
//  const size_t src_len;
//}* Scanner;
//
//
//typedef enum TokenType_E {
//  Eof = '\0',
//  Space = ' ',
//  Tab = '\t',
//  NewLine = '\n',
//  LeftParen = '(',
//  RightParen = ')',
//  LeftBracket = '[',
//  IntType = 'I',
//  BoolType = 'Z',
//  CharType = 'C',
//  ShortType = 'S',
//  LongType = 'L',
//  FloatType = 'F',
//  DoubleType = 'D',
//  ObjectType = 'L',
//  VoidType = 'V',
//  SemiColon = ';',
//  ForwardSlash = '/',
//  Other = 126,
//} TokenType;
//
//typedef struct Token_S {
//  TokenType Type;
//  char Val;
//  const char* __nullable Literal;
//}* Token;
//
//
//void scanner_skip_whitespace(Scanner scanner) {
//  while (scanner->src[scanner->index] == Tab || scanner->src[scanner->index] == NewLine || scanner->src[scanner->index] == Space) {
//    scanner->index++;
//    if (scanner->index == scanner->src_len || scanner->src[scanner->index] == Eof) {
//      return;
//    }
//  }
//}
//
//void scanner_advance_until(Scanner scanner, TokenType type) {
//  scanner_skip_whitespace(scanner);
//  if (scanner->index == scanner->src_len || scanner->src[scanner->index] == Eof) {
//    return;
//  }
//
//  while (scanner->src[scanner->index] != type) {
//    scanner->index++;
//    if (scanner->index == scanner->src_len || scanner->src[scanner->index] == Eof) {
//      return;
//    }
//  }
//
//}
//
//struct Token_S scanner_read(Scanner scanner) {
//
//  scanner_skip_whitespace(scanner);
//  if (scanner->index == scanner->src_len || scanner->src[scanner->index] == Eof) {
//    return (struct Token_S) {
//        .Type = Eof,
//        .Val = '\0'
//    };
//  }
//
//
//  char val = scanner->src[scanner->index++];
//  TokenType type = Illegal;
//  switch ((TokenType) val) {
//    LeftBracket
//
//  }
//
//
//  return (struct Token_S) {
//
//  };
//}
//
//typedef enum NodeType_E {
//  Illegal_Node,
//  Primitive_Node,
//  Array_Node,
//  Object_Node,
//} NodeType;
//
//typedef union Node_U* Node;
//
//typedef struct PrimitiveNode_S {
//  NodeType type;
//  TokenType token_type;
//}* PrimitiveNode;
//
//typedef struct ArrayNode_S {
//  NodeType type;
//  Node array_of;
//}* ArrayNode;
//
//typedef struct ReturnNode_S {
//  NodeType type;
//  Node return_type;
//}* ReturnNode;
//
//typedef struct ObjectNode_S {
//  NodeType type;
//  const char* path;
//}* ObjectNode;
//
//union Node_U {
//  struct PrimitiveNode_S primitive;
//  struct ArrayNode_S array;
//  struct ObjectNode_S object;
//  struct ReturnNode_S ret;
//};
//
//struct Parser_S {
//  struct Scanner_S scanner;
//  size_t nodes_amnt;
//  union Node_U Nodes[2048];
//}* Parser;
//
//
//union Node_U parser_load_node(Scanner scanner, Token token) {
//  if (token->Type == ObjectType) {
//    const size_t start = scanner->index;
//    advance_until(scanner, SemiColon);
//    const size_t strLen = (scanner->index - start);
//    char* typeStr = malloc(sizeof(char) * (strLen + 1));
//    memcpy(typeStr, scanner->src + start, strLen);
//    typeStr[strLen] = '\0';
//    return (union Node_U) {
//      .object = {
//          .type = Object_Node,
//          .path = typeStr
//      }
//    };
//  }
//  if (token->Type == LeftBracket) {
//    if []
//  }
//}
//
//
//struct Parser_S Parse(const char* src, size_t srcLen) {
//  struct Parser_S parser = {
//    .scanner = {
//        .src = src,
//        .src_len = srcLen
//    }
//  };
//  struct Token_S token;
//  while ((token = scanner_read(&parser.scanner)).Type != Eof) {
//    if (token.Type == LeftBracket) {
//
//    }
//  }
//
//}
